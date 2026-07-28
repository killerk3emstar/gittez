using GitBounty.Core.Models;

namespace GitBounty.Core.Scoring;

public static class RepoScorer
{
    public static RepoScore Score(
        RepoCandidate repo,
        UserProfile user,
        IReadOnlyList<int> poolSizes,
        int targetStars,
        HealthInput? health,
        DateTimeOffset now)
    {
        var matchParts = MatchScorer.Components(repo, user, poolSizes, targetStars);

        // Community Fit zawsze ma punkty, więc Match nie może wyjść null
        var match = ScoreMath.Renormalize(matchParts) ?? 0;

        IReadOnlyList<ScoreComponent> healthParts = health is null
            ? []
            : HealthScorer.Components(health, now);

        var healthScore = ScoreMath.Renormalize(healthParts);

        return new RepoScore(
            match,
            matchParts,
            healthScore,
            healthParts,
            ScoreMath.Final(match, healthScore));
    }
}
