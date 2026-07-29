using System.Collections.Concurrent;
using Gittez.Core.Abstractions;
using Gittez.Core.Models;
using Gittez.Core.Profiles;
using Gittez.Core.Scoring;

namespace Gittez.Core.Recommendations;

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

        try
        {
            return await RunLiveAsync(request, profile, profileIsStale, languages, starsLo, starsHi, now, ct);
        }
        catch (GitHubUnavailableException)
        {
            // Dopóki cache cokolwiek zawiera, serwujemy stare dane - puste demo
            // z komunikatem o błędzie jest gorsze niż lekko nieświeże dane.
            // Klamra obejmuje cały przebieg, nie samo wyszukiwanie: limit potrafi
            // się wyczerpać dopiero przy pobieraniu issues.
            return await FromCacheAsync(profile, request, languages, starsLo, starsHi, ct);
        }
    }

    async Task<RecommendationResult> RunLiveAsync(
        RecommendationRequest request,
        UserProfile profile,
        bool profileIsStale,
        IReadOnlyList<string> languages,
        int starsLo,
        int starsHi,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var pool = await SearchAsync(languages, starsLo, starsHi, ct);

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
            if (await TryGetIssuesAsync(repo, token) is not { } fetched) return null;

            var free = fetched.Issues.Where(i => IsTakeable(i, request)).ToArray();

            // Repozytorium bez ani jednego wolnego issue wypada z listy: fakt
            // istnienia issue jest filtrem, nie punktami (SPEC §0.2).
            return free.Length == 0 ? null : new FinalistIssues(repo, free, fetched.FetchedAt);
        }, ct);

        if (withFreeIssues.Count == 0)
        {
            return new RecommendationResult(profile, [], profileIsStale, Hints(request, foundCandidates: true));
        }

        var scored = await ForEachAsync(withFreeIssues, async (entry, token) =>
        {
            var (health, healthComputedAt) = await GetHealthAsync(entry.Repo, now, token);

            var score = RepoScorer.Score(entry.Repo, profile, poolSizes, request.TargetStars, health);

            return new Recommendation(entry.Repo, score, entry.Issues, entry.FetchedAt, healthComputedAt);
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
            var result = await profiles.GetAsync(login, ct);
            return (result.Profile, result.IsStale);
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

    // Repozytorium, które zniknęło albo nie odpowiedziało, wypada z listy zamiast
    // wywracać cały przebieg - tak samo jak przy Health. Niedostępność całego
    // GitHuba leci wyżej, bo tam odpowiedzią jest cache, a nie krótsza lista.
    async Task<(IReadOnlyList<ScoredIssue> Issues, DateTimeOffset FetchedAt)?> TryGetIssuesAsync(
        RepoCandidate repo, CancellationToken ct)
    {
        try
        {
            return await GetIssuesAsync(repo, ct);
        }
        catch (GitHubUnavailableException)
        {
            throw;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    static bool IsTakeable(ScoredIssue issue, RecommendationRequest request) =>
        !issue.Issue.HasAssignee
        && (request.MaxDifficulty is null || issue.Difficulty <= request.MaxDifficulty);

    async Task<(IReadOnlyList<ScoredIssue> Issues, DateTimeOffset FetchedAt)> GetIssuesAsync(
        RepoCandidate repo, CancellationToken ct)
    {
        var cached = await cache.GetIssuesAsync(repo.FullName, ct);
        if (cached is { IsFresh: true }) return (cached.Issues, cached.FetchedAt);

        var result = await github.GetGoodFirstIssuesAsync(repo.FullName, cached?.ETag, ct);

        // 304 nie zmniejsza limitu i oznacza, że cache jest nadal aktualny -
        // więc znacznik idzie do przodu, inaczej dane wyglądałyby na stare mimo
        // świeżej walidacji, a TTL wygasałby co godzinę bez końca.
        if (result.NotModified && cached is not null)
        {
            await cache.TouchIssuesAsync(repo.FullName, ct);
            return (cached.Issues, time.GetUtcNow());
        }

        IReadOnlyList<ScoredIssue> issues =
            [.. (result.Value ?? []).Select(i => new ScoredIssue(i, DifficultyHeuristic.Estimate(i)))];

        await cache.SaveIssuesAsync(repo, issues, result.ETag, ct);
        return (issues, time.GetUtcNow());
    }

    async Task<(IReadOnlyList<ScoreComponent>? Components, DateTimeOffset? ComputedAt)> GetHealthAsync(
        RepoCandidate repo, DateTimeOffset now, CancellationToken ct)
    {
        var cached = await cache.GetHealthAsync(repo.FullName, ct);
        if (cached is { IsFresh: true }) return (cached.Breakdown, cached.ComputedAt);

        try
        {
            var input = await github.GetHealthInputAsync(repo.FullName, repo.PushedAt, ct);
            var components = HealthScorer.Components(input, now);
            var score = ScoreMath.Renormalize(components);

            await cache.SaveHealthAsync(repo, score, components, ct);
            return (components, now);
        }
        catch (GitHubUnavailableException)
        {
            return (cached?.Breakdown, cached?.ComputedAt);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Pojedyncze repozytorium bez Health nadal trafia do wyniku - ocena
            // procentuje się po dostępnych komponentach, a Match ma komplet.
            return (cached?.Breakdown, cached?.ComputedAt);
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

        // Ten sam filtr co na żywo: prośba o łatwe issues nie może zniknąć tylko
        // dlatego, że dane przyszły z cache'u.
        var usable = cached
            .Select(c => (Candidate: c, Issues: c.Issues.Where(i => IsTakeable(i, request)).ToArray()))
            .Where(x => x.Issues.Length > 0)
            .ToArray();

        if (usable.Length == 0)
        {
            IReadOnlyList<string> hints = cached.Count == 0
                ? ["Nie mamy świeżych danych z GitHuba ani niczego w cache'u dla tych języków."]
                : [.. Hints(request, foundCandidates: true)
                    .Prepend("Odpowiadamy z cache'u, bo GitHub jest teraz niedostępny.")];

            return new RecommendationResult(profile, [], true, hints);
        }

        var poolSizes = usable.Select(x => x.Candidate.Repo.SizeKb).ToArray();

        var items = usable
            .Select(x => new Recommendation(
                x.Candidate.Repo,
                RepoScorer.Score(
                    x.Candidate.Repo, profile, poolSizes, request.TargetStars, x.Candidate.Health?.Breakdown),
                x.Issues,
                x.Candidate.FetchedAt,
                x.Candidate.Health?.ComputedAt))
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

sealed record FinalistIssues(
    RepoCandidate Repo, IReadOnlyList<ScoredIssue> Issues, DateTimeOffset FetchedAt);
