using GitBounty.Api.Contracts;
using GitBounty.Core.Recommendations;

namespace GitBounty.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/recommendations", async (
            string login,
            RecommendationPipeline pipeline,
            TimeProvider time,
            string? languages,
            int targetStars,
            int? maxDifficulty,
            int? limit,
            CancellationToken ct) =>
        {
            var request = new RecommendationRequest(
                login,
                [.. (languages ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                targetStars is > 0 ? targetStars : 500,
                maxDifficulty,
                Math.Clamp(limit ?? 10, 1, 25));

            var result = await pipeline.RunAsync(request, ct);
            var now = time.GetUtcNow();

            return Results.Ok(new RecommendationsResponse(
                [.. result.Items.Select(item => ToItem(item, now))],
                result.Hints));
        })
        .WithName("GetRecommendations")
        .WithSummary("Dziesięć rekomendacji z rozbiciem Match i Health");

        return app;
    }

    static RecommendationItem ToItem(Recommendation recommendation, DateTimeOffset now)
    {
        var repo = recommendation.Repo;
        var score = recommendation.Score;

        return new RecommendationItem(
            repo.FullName,
            repo.Description,
            repo.HtmlUrl,
            repo.Stars,
            repo.PrimaryLanguage,
            repo.Topics,
            repo.PushedAt,
            Math.Round(score.MatchScore, 1),
            score.HealthScore is { } health ? Math.Round(health, 1) : null,
            Math.Round(score.FinalScore, 1),
            score.MatchBreakdown,
            score.HealthBreakdown,
            [.. recommendation.Issues.Select(i => new IssueResponse(
                i.Issue.Number,
                i.Issue.Title,
                i.Issue.HtmlUrl,
                i.Issue.Labels,
                i.Issue.CommentCount,
                i.Difficulty,
                i.Issue.UpdatedAt))],
            new DataFreshness(now, score.HealthScore is null ? null : now));
    }
}
