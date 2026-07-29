using Gittez.Core.Models;

namespace Gittez.Core.Scoring;

public static class RepoScorer
{
    // Komponenty Health przychodzą policzone: albo świeżo z HealthScorer, albo
    // z cache'u, gdzie leżą w tej samej postaci (repo_cache.health_breakdown).
    public static RepoScore Score(
        RepoCandidate repo,
        UserProfile user,
        IReadOnlyList<int> poolSizes,
        int targetStars,
        IReadOnlyList<ScoreComponent>? healthComponents)
    {
        var matchParts = MatchScorer.Components(repo, user, poolSizes, targetStars);

        // Community Fit zawsze ma punkty, więc Match nie może wyjść null
        var match = ScoreMath.Renormalize(matchParts) ?? 0;

        var healthParts = healthComponents ?? [];
        var healthScore = ScoreMath.Renormalize(healthParts);

        return new RepoScore(
            match,
            matchParts,
            healthScore,
            healthParts,
            ScoreMath.Final(match, healthScore));
    }
}
