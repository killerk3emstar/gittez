using System.Collections.Concurrent;
using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;
using GitBounty.Core.Profiles;
using GitBounty.Core.Scoring;

namespace GitBounty.Core.Recommendations;

// Kroki 1-7 z SPEC §5. Zależy wyłącznie od interfejsów, więc Core zostaje bez
// zależności, a testy nie dotykają ani sieci, ani bazy.
public sealed class RecommendationPipeline(
    IGitHubClient github,
    IRepoCache cache,
    ProfileProvider profiles,
    TimeProvider time)
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
        var now = time.GetUtcNow();
        var (profile, profileIsStale) = await GetProfileAsync(request.Login, ct);

        var languages = request.Languages.Count > 0
            ? request.Languages
            : [.. profile.Languages.Take(DefaultLanguages).Select(l => l.Name)];

        if (languages.Count == 0)
        {
            return new RecommendationResult(profile, [], profileIsStale,
                ["Nie wykryliśmy żadnego języka w Twoich repozytoriach, wskaż go ręcznie."]);
        }

        var (starsLo, starsHi) = ScoreMath.StarBand(request.TargetStars);

        IReadOnlyList<RepoCandidate> pool;
        try
        {
            pool = await SearchAsync(languages, starsLo, starsHi, ct);
        }
        catch (GitHubUnavailableException)
        {
            // Dopóki cache cokolwiek zawiera, serwujemy stare dane - puste demo
            // z komunikatem o błędzie jest gorsze niż lekko nieświeże dane.
            return await FromCacheAsync(profile, request, languages, starsLo, starsHi, ct);
        }

        if (pool.Count == 0)
        {
            return new RecommendationResult(profile, [], profileIsStale, Hints(request, foundCandidates: false));
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
            var issues = await GetIssuesAsync(repo, token);

            var free = issues
                .Where(i => !i.Issue.HasAssignee)
                .Where(i => request.MaxDifficulty is null || i.Difficulty <= request.MaxDifficulty)
                .ToArray();

            // Repozytorium bez ani jednego wolnego issue wypada z listy: fakt
            // istnienia issue jest filtrem, nie punktami (SPEC §0.2).
            return free.Length == 0 ? null : new FinalistIssues(repo, free);
        }, ct);

        if (withFreeIssues.Count == 0)
        {
            return new RecommendationResult(profile, [], profileIsStale, Hints(request, foundCandidates: true));
        }

        var scored = await ForEachAsync(withFreeIssues, async (entry, token) =>
        {
            var health = await GetHealthAsync(entry.Repo, now, token);

            var score = RepoScorer.Score(entry.Repo, profile, poolSizes, request.TargetStars, health);

            return new Recommendation(entry.Repo, score, entry.Issues);
        }, ct);

        var items = scored
            .OrderByDescending(r => r.Score.FinalScore)
            .Take(request.Limit)
            .ToArray();

        return new RecommendationResult(profile, items, profileIsStale, []);
    }

    async Task<(UserProfile Profile, bool IsStale)> GetProfileAsync(string login, CancellationToken ct)
    {
        try
        {
            return await profiles.GetAsync(login, ct);
        }
        catch (GitHubUnavailableException)
        {
            // Bez tokenu i bez cache'u nie wykryjemy języków; profil zostaje
            // pusty, a języki biorą się z tego, co zaznaczył użytkownik.
            return (new UserProfile(login, [], 0, [], 0), true);
        }
    }

    async Task<IReadOnlyList<RepoCandidate>> SearchAsync(
        IReadOnlyList<string> languages, int starsLo, int starsHi, CancellationToken ct)
    {
        var candidates = new Dictionary<string, RepoCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var language in languages.Take(MaxLanguages))
        {
            foreach (var candidate in await github.SearchRepositoriesAsync(language, starsLo, starsHi, ct))
            {
                candidates.TryAdd(candidate.FullName, candidate);
            }
        }

        // Większość filtrów załatwia już zapytanie (SPEC §5 krok 3).
        return [.. candidates.Values.Where(IsUsable)];
    }

    async Task<IReadOnlyList<ScoredIssue>> GetIssuesAsync(RepoCandidate repo, CancellationToken ct)
    {
        var cached = await cache.GetIssuesAsync(repo.FullName, ct);
        if (cached is { IsFresh: true }) return cached.Issues;

        var result = await github.GetGoodFirstIssuesAsync(repo.FullName, cached?.ETag, ct);

        // 304 nie zmniejsza limitu i oznacza, że cache jest nadal aktualny
        if (result.NotModified && cached is not null) return cached.Issues;

        IReadOnlyList<ScoredIssue> issues =
            [.. (result.Value ?? []).Select(i => new ScoredIssue(i, DifficultyHeuristic.Estimate(i)))];

        await cache.SaveIssuesAsync(repo, issues, result.ETag, ct);
        return issues;
    }

    async Task<IReadOnlyList<ScoreComponent>?> GetHealthAsync(
        RepoCandidate repo, DateTimeOffset now, CancellationToken ct)
    {
        var cached = await cache.GetHealthAsync(repo.FullName, ct);
        if (cached is { IsFresh: true }) return cached.Breakdown;

        try
        {
            var input = await github.GetHealthInputAsync(repo.FullName, repo.PushedAt, ct);
            var components = HealthScorer.Components(input, now);
            var score = ScoreMath.Renormalize(components);

            await cache.SaveHealthAsync(repo, score, components, ct);
            return components;
        }
        catch (GitHubUnavailableException)
        {
            return cached?.Breakdown;
        }
        catch (Exception)
        {
            // Pojedyncze repozytorium bez Health nadal trafia do wyniku - ocena
            // procentuje się po dostępnych komponentach, a Match ma komplet.
            return cached?.Breakdown;
        }
    }

    async Task<RecommendationResult> FromCacheAsync(
        UserProfile profile,
        RecommendationRequest request,
        IReadOnlyList<string> languages,
        int starsLo,
        int starsHi,
        CancellationToken ct)
    {
        var cached = await cache.GetCandidatesAsync(languages, starsLo, starsHi, FinalistCount * 4, ct);

        var usable = cached
            .Where(c => c.Issues.Any(i => !i.Issue.HasAssignee))
            .ToArray();

        if (usable.Length == 0)
        {
            return new RecommendationResult(profile, [], true,
                ["Nie mamy świeżych danych z GitHuba ani niczego w cache'u dla tych języków."]);
        }

        var poolSizes = usable.Select(c => c.Repo.SizeKb).ToArray();

        var items = usable
            .Select(c => new Recommendation(
                c.Repo,
                RepoScorer.Score(c.Repo, profile, poolSizes, request.TargetStars, c.Health?.Breakdown),
                [.. c.Issues.Where(i => !i.Issue.HasAssignee)]))
            .OrderByDescending(r => r.Score.FinalScore)
            .Take(request.Limit)
            .ToArray();

        return new RecommendationResult(profile, items, true, []);
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
