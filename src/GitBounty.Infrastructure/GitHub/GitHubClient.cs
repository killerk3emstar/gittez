using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GitBounty.Infrastructure.GitHub;

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

    public async Task<GitHubResult<IReadOnlyList<IssueSummary>>> GetFreeGoodFirstIssuesAsync(
        string fullName, string? etag = null, CancellationToken ct = default)
    {
        var label = Uri.EscapeDataString("good first issue");
        var result = await GetAsync<List<IssueDto>>(
            $"repos/{fullName}/issues?labels={label}&state=open&per_page=20", etag, ct);

        if (result.NotModified) return new(null, result.ETag, true);

        IReadOnlyList<IssueSummary> issues =
        [
            .. (result.Value ?? [])
                .Where(i => i.PullRequest is null)
                .Where(i => (i.Assignees?.Count ?? 0) == 0)
                .Select(ToIssue)
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
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrEmpty(etag)) request.Headers.TryAddWithoutValidation("If-None-Match", etag);

        using var response = await http.SendAsync(request, ct);
        rateLimit.Observe(response);

        logger.LogDebug("GET {Path} -> {Status}, limit {Remaining}/{Limit}",
            path, (int)response.StatusCode, rateLimit.Current?.Remaining, rateLimit.Current?.Limit);

        // 304 nie zmniejsza limitu
        if (response.StatusCode == HttpStatusCode.NotModified) return new(default, etag, true);

        if (response.StatusCode == HttpStatusCode.NotFound) throw new GitHubNotFoundException(path);

        if (IsRateLimited(response))
        {
            throw new GitHubRateLimitExceededException(
                rateLimit.Current?.ResetAt ?? DateTimeOffset.UtcNow.AddMinutes(1));
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

sealed class GitHubNotFoundException(string path) : Exception($"GitHub zwrócił 404 dla {path}");
