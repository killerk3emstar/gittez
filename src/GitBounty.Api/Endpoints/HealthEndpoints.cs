using GitBounty.Api.Contracts;
using GitBounty.Core.Abstractions;
using GitBounty.Infrastructure.Persistence;

namespace GitBounty.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (
            GitBountyDbContext db,
            IRateLimitTracker rateLimit,
            CancellationToken ct) =>
        {
            bool canConnect;
            string? error = null;
            try
            {
                canConnect = await db.Database.CanConnectAsync(ct);
            }
            catch (Exception ex)
            {
                canConnect = false;
                error = ex.Message;
            }

            var limit = rateLimit.Current;

            var response = new HealthResponse(
                canConnect ? "healthy" : "degraded",
                new DatabaseHealth(canConnect, error),
                limit is null ? null : new RateLimitHealth(limit.Remaining, limit.Used, limit.ResetAt));

            return Results.Json(response, statusCode: canConnect ? 200 : 503);
        })
        .WithName("GetHealth")
        .WithSummary("Status bazy i pozostałego limitu GitHub API");

        return app;
    }
}
