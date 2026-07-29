namespace Gittez.Core.Models;

public sealed record RepoScore(
    double MatchScore,
    IReadOnlyList<ScoreComponent> MatchBreakdown,
    // null gdy wszystkie komponenty Health są null - wtedy FinalScore to sam Match
    double? HealthScore,
    IReadOnlyList<ScoreComponent> HealthBreakdown,
    double FinalScore);
