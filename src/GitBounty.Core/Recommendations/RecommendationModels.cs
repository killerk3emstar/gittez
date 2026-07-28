using GitBounty.Core.Models;

namespace GitBounty.Core.Recommendations;

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
    IReadOnlyList<ScoredIssue> Issues);

public sealed record RecommendationResult(
    UserProfile Profile,
    IReadOnlyList<Recommendation> Items,
    IReadOnlyList<string> Hints);
