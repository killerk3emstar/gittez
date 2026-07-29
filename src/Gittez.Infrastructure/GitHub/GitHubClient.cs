using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gittez.Core.Abstractions;
using Gittez.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Gittez.Infrastructure.GitHub;

public sealed class GitHubClient(
    HttpClient http,
    RateLimitTracker rateLimit,
    SearchThrottle searchThrottle,
    IOptions<GitHubOptions> options,
    ILogger<GitHubClient> logger) : IGitHubClient
{
    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    readonly GitHubOptions _options = options.Value;

    public async Task<IReadOnlyList<OwnedRepo>> GetOwnedReposAsync(string login, CancellationToken ct = default)
    {
        try
        {
            var repos = await GetAsync<List<RepoDto>>(
                $"users/{Uri.EscapeDataString(login)}/repos?per_page=100&sort=pushed", etag: null, ct);

            return [.. (repos.Value ?? []).Select(r =>
                new OwnedRepo(r.FullName, r.Language, r.Size, r.Topics ?? [], r.Fork))];
        }
        catch (GitHubNotFoundException)
        {
            throw new GitHubUserNotFoundException(login);
        }
    }

    // Bez parametru sort: sortowanie kształtuje wyniki mocniej niż filtry,
    // a sort=stars zeruje Community Fit i Complexity Match (SPEC §0.4).
    public async Task<IReadOnlyList<RepoCandidate>> SearchRepositoriesAsync(
        string language, int starsLo, int starsHi, CancellationToken ct = default)
    {
        var pushedAfter = DateTimeOffset.UtcNow.AddDays(-_options.MaxPushedAgeDays).ToString("yyyy-MM-dd");
        var query =
            $"language:{language} good-first-issues:>=2 stars:{starsLo}..{starsHi} " +
            $"pushed:>{pushedAfter} archived:false fork:false";

        var result = await searchThrottle.RunAsync(
            () => GetAsync<SearchResponseDto>(
                $"search/repositories?q={Uri.EscapeDataString(query)}&per_page=100", etag: null, ct),
            ct);

        var items = result.Value?.Items ?? [];
        logger.LogInformation("Search {Language} {Lo}..{Hi} zwrócił {Count} z {Total}",
            language, starsLo, starsHi, items.Count, result.Value?.TotalCount ?? 0);

        return [.. items.Select(ToCandidate)];
    }

    public async Task<GitHubResult<IReadOnlyList<IssueSummary>>> GetGoodFirstIssuesAsync(
        string fullName, string? etag = null, CancellationToken ct = default)
    {
        var label = Uri.EscapeDataString("good first issue");
        var result = await GetAsync<List<IssueDto>>(
            $"repos/{fullName}/issues?labels={label}&state=open&per_page=20", etag, ct);

        if (result.NotModified) return new(null, result.ETag, true);

        // Odsiew po przypisaniu robi pipeline, tutaj wypadają tylko elementy,
        // które w istocie są pull requestami (SPEC §4.4 pkt 5).
        IReadOnlyList<IssueSummary> issues =
        [
            .. (result.Value ?? []).Where(i => i.PullRequest is null).Select(ToIssue)
        ];

        return new(issues, result.ETag, false);
    }

    public async Task<HealthInput> GetHealthInputAsync(
        string fullName, DateTimeOffset pushedAt, CancellationToken ct = default)
    {
        // Sekwencyjnie w obrębie repozytorium: równoległość jest po stronie
        // pipeline'u (8 repozytoriów naraz), a GitHub dławi współbieżność.
        var recent = await GetAsync<List<PullDto>>(
            $"repos/{fullName}/pulls?state=all&per_page=30&sort=updated", etag: null, ct);

        var open = await GetAsync<List<PullDto>>(
            $"repos/{fullName}/pulls?state=open&sort=created&direction=asc&per_page=100", etag: null, ct);

        var closed = await GetAsync<List<IssueDto>>(
            $"repos/{fullName}/issues?state=closed&per_page=30", etag: null, ct);

        return new HealthInput(
            [.. (recent.Value ?? []).Select(ToPull)],
            [.. (open.Value ?? []).Select(ToPull)],
            [.. (closed.Value ?? []).Select(i => new ClosedIssue(i.CreatedAt, i.ClosedAt, i.PullRequest is not null))],
            pushedAt);
    }

    async Task<GitHubResult<T>> GetAsync<T>(string path, string? etag, CancellationToken ct)
    {
        try
        {
            return await SendAsync<T>(path, etag, ct);
        }
        catch (Exception ex) when (IsTransportFailure(ex, ct))
        {
            throw new GitHubTransportException($"GitHub nie odpowiedział na {path}: {ex.Message}", ex);
        }
    }

    // Zerwane połączenie, timeout Polly i przerwane czytanie odpowiedzi wyglądają
    // inaczej, a znaczą to samo. Anulowanie żądania przez klienta awarią nie jest.
    static bool IsTransportFailure(Exception ex, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return false;

        return ex switch
        {
            // Puste StatusCode znaczy, że odpowiedź w ogóle nie przyszła: DNS,
            // odmowa połączenia, zerwane TLS. Z ustawionym przyszedł błąd HTTP
            // i ten rozstrzygamy wyżej, po statusie.
            HttpRequestException { StatusCode: null } => true,
            TimeoutRejectedException or TaskCanceledException => true,
            _ => false,
        };
    }

    async Task<GitHubResult<T>> SendAsync<T>(string path, string? etag, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrEmpty(etag)) request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        using var response = await http.SendAsync(request, ct);

        var pool = path.StartsWith("search/", StringComparison.Ordinal) ? RateLimitPool.Search : RateLimitPool.Core;
        rateLimit.Observe(response, pool);

        logger.LogDebug("GET {Path} -> {Status}, pula {Pool}, zostało {Remaining}",
            path, (int)response.StatusCode, pool,
            (pool == RateLimitPool.Search ? rateLimit.Search : rateLimit.Core)?.Remaining);

        // 304 nie zmniejsza limitu
        if (response.StatusCode == HttpStatusCode.NotModified) return new(default, etag, true);

        if (response.StatusCode == HttpStatusCode.NotFound) throw new GitHubNotFoundException(path);

        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new GitHubUnauthorizedException();

        if (IsRateLimited(response))
        {
            throw new GitHubRateLimitExceededException(
                (pool == RateLimitPool.Search ? rateLimit.Search : rateLimit.Core)?.ResetAt
                    ?? DateTimeOffset.UtcNow.AddMinutes(1));
        }

        // 5xx przeżyło już trzy ponowienia (GitHubResilience), więc to nie jest
        // chwilowa czkawka - lepiej oddać cache niż błąd.
        if ((int)response.StatusCode >= 500)
        {
            throw new GitHubTransportException($"GitHub zwrócił {(int)response.StatusCode} dla {path}");
        }

        response.EnsureSuccessStatusCode();

        var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return new(value, response.Headers.ETag?.Tag, false);
    }

    static bool IsRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)) return false;

        return !response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            || values.FirstOrDefault() == "0";
    }

    static RepoCandidate ToCandidate(RepoDto r) => new(
        r.FullName, r.Description, r.HtmlUrl, r.StargazersCount, r.Language, r.Topics ?? [],
        r.Size, r.PushedAt, r.License?.SpdxId, r.Archived, r.Disabled, r.HasIssues, r.OpenIssuesCount);

    static IssueSummary ToIssue(IssueDto i) => new(
        i.Id, i.Number, i.Title, i.HtmlUrl,
        [.. (i.Labels ?? []).Select(l => l.Name)],
        i.Comments, i.Body?.Length ?? 0,
        HasAssignee: (i.Assignees?.Count ?? 0) > 0,
        i.CreatedAt, i.UpdatedAt);

    static PullSummary ToPull(PullDto p) => new(
        p.CreatedAt, p.ClosedAt, p.MergedAt, p.Draft,
        p.User?.Login ?? string.Empty, p.User?.Type ?? "User");
}
