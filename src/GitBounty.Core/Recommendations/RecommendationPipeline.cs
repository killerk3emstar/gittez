using System.Collections.Concurrent;
using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;
using GitBounty.Core.Profiles;
using GitBounty.Core.Scoring;

namespace GitBounty.Core.Recommendations;

// Kroki 1-7 z SPEC §5. Zależy wyłącznie od interfejsu klienta, więc Core
// zostaje bez zależności, a testy nie dotykają sieci.
public sealed class RecommendationPipeline(IGitHubClient github, TimeProvider time)
{
    // 25, nie 20: krok 5 odsiewa repozytoria bez wolnych issues, więc lejek
    // się zwęża i przy 20 finalistach brakuje zapasu na dziesiątkę wyjściową.
    const int FinalistCount = 25;

    // Zmierzone: 16 wątków nie daje nic, GitHub dławi współbieżność (SPEC §2.2).
    const int MaxParallelism = 8;

    const int MaxLanguages = 4;
    const int DefaultLanguages = 3;

    public async Task<RecommendationResult> RunAsync(RecommendationRequest request, CancellationToken ct = default)
    {
        var ownedRepos = await github.GetOwnedReposAsync(request.Login, ct);
        var profile = ProfileBuilder.Build(request.Login, ownedRepos);

        var languages = request.Languages.Count > 0
            ? request.Languages
            : [.. profile.Languages.Take(DefaultLanguages).Select(l => l.Name)];

        if (languages.Count == 0)
        {
            return new RecommendationResult(profile, [], ["Nie wykryliśmy żadnego języka w Twoich repozytoriach, wskaż go ręcznie."]);
        }

        var (starsLo, starsHi) = ScoreMath.StarBand(request.TargetStars);

        var candidates = new Dictionary<string, RepoCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages.Take(MaxLanguages))
        {
            foreach (var candidate in await github.SearchRepositoriesAsync(language, starsLo, starsHi, ct))
            {
                candidates.TryAdd(candidate.FullName, candidate);
            }
        }

        var pool = candidates.Values.Where(IsUsable).ToArray();
        if (pool.Length == 0)
        {
            return new RecommendationResult(profile, [], Hints(request, foundCandidates: false));
        }

        var poolSizes = pool.Select(c => c.SizeKb).ToArray();

        var finalists = pool
            .Select(repo => (Repo: repo, Match: MatchScorer.Score(repo, profile, poolSizes, request.TargetStars) ?? 0))
            .OrderByDescending(x => x.Match)
            .Take(FinalistCount)
            .Select(x => x.Repo)
            .ToArray();

        var withFreeIssues = await ForEachAsync(finalists, async (repo, token) =>
        {
            var result = await github.GetFreeGoodFirstIssuesAsync(repo.FullName, etag: null, token);

            var issues = (result.Value ?? [])
                .Select(issue => new ScoredIssue(issue, DifficultyHeuristic.Estimate(issue)))
                .Where(issue => request.MaxDifficulty is null || issue.Difficulty <= request.MaxDifficulty)
                .ToArray();

            // Repozytorium bez ani jednego wolnego issue wypada z listy: fakt
            // istnienia issue jest filtrem, nie punktami (SPEC §0.2).
            return issues.Length == 0 ? null : new FinalistIssues(repo, issues);
        }, ct);

        if (withFreeIssues.Count == 0)
        {
            return new RecommendationResult(profile, [], Hints(request, foundCandidates: true));
        }

        var scored = await ForEachAsync(withFreeIssues, async (entry, token) =>
        {
            var health = await TryGetHealthAsync(entry.Repo, token);

            var score = RepoScorer.Score(
                entry.Repo, profile, poolSizes, request.TargetStars, health, time.GetUtcNow());

            return new Recommendation(entry.Repo, score, entry.Issues);
        }, ct);

        var items = scored
            .OrderByDescending(r => r.Score.FinalScore)
            .Take(request.Limit)
            .ToArray();

        return new RecommendationResult(profile, items, []);
    }

    async Task<HealthInput?> TryGetHealthAsync(RepoCandidate repo, CancellationToken ct)
    {
        try
        {
            return await github.GetHealthInputAsync(repo.FullName, repo.PushedAt, ct);
        }
        catch (GitHubRateLimitExceededException)
        {
            throw;
        }
        catch (Exception)
        {
            // Pojedyncze repozytorium bez Health nadal trafia do wyniku - ocena
            // procentuje się po dostępnych komponentach, a Match ma komplet.
            return null;
        }
    }

    static async Task<List<T>> ForEachAsync<TSource, T>(
        IReadOnlyList<TSource> source,
        Func<TSource, CancellationToken, Task<T?>> body,
        CancellationToken ct)
        where T : class
    {
        var results = new ConcurrentBag<T>();

        await Parallel.ForEachAsync(
            source,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism, CancellationToken = ct },
            async (item, token) =>
            {
                var result = await body(item, token);
                if (result is not null) results.Add(result);
            });

        return [.. results];
    }

    // Większość filtrów załatwia już zapytanie (SPEC §5 krok 3).
    static bool IsUsable(RepoCandidate repo) => !repo.Archived && !repo.Disabled && repo.HasIssues;

    static IReadOnlyList<string> Hints(RecommendationRequest request, bool foundCandidates)
    {
        var hints = new List<string>();

        if (foundCandidates)
        {
            hints.Add("Wszystkie znalezione repozytoria mają zajęte issues, spróbuj innego pasma wielkości.");
        }
        else
        {
            hints.Add("Zmień preferowaną wielkość projektu, żeby przeszukać inne pasmo gwiazdek.");
        }

        if (request.Languages.Count < 3) hints.Add("Zaznacz dodatkowy język.");
        if (request.MaxDifficulty is not null) hints.Add("Poluzuj maksymalną trudność issues.");

        return hints;
    }
}

sealed record FinalistIssues(RepoCandidate Repo, IReadOnlyList<ScoredIssue> Issues);
