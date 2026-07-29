using Gittez.Core.Models;

namespace Gittez.Core.Recommendations;

public sealed record RecommendationRequest(
    string Login,
    IReadOnlyList<string> Languages,
    // Sterowanie wyszukiwaniem, nie tylko wagą: wyznacza pasmo stars:{lo}..{hi}
    // w zapytaniu, więc przesunięcie suwaka zwraca inne repozytoria (SPEC §0.4).
    int TargetStars = 500,
    int? MaxDifficulty = null,
    int Limit = 10);

public sealed record ScoredIssue(IssueSummary Issue, int Difficulty);

public sealed record Recommendation(
    RepoCandidate Repo,
    RepoScore Score,
    IReadOnlyList<ScoredIssue> Issues,
    // Znaczniki z cache'u, nie czas odpowiedzi: banner „dane sprzed X godzin"
    // ma mówić, kiedy dane powstały, a nie kiedy je przepisaliśmy do JSON-a.
    DateTimeOffset RepoFetchedAt,
    DateTimeOffset? HealthComputedAt);

public sealed record RecommendationResult(
    UserProfile Profile,
    IReadOnlyList<Recommendation> Items,
    // Odpowiedź poszła z cache'u mimo wygasłego TTL, bo GitHub był
    // niedostępny; API dokłada wtedy nagłówek X-Data-Stale (SPEC §7.3).
    bool IsStale,
    IReadOnlyList<string> Hints);
