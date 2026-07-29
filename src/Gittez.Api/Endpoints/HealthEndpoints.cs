using Gittez.Api.Contracts;
using Gittez.Core.Abstractions;
using Gittez.Core.Models;
using Gittez.Infrastructure.Persistence;

namespace Gittez.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (
            GittezDbContext db,
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

            var response = new HealthResponse(
                canConnect ? "healthy" : "degraded",
                new DatabaseHealth(canConnect, error),
                new RateLimitHealth(Pool(rateLimit.Core), Pool(rateLimit.Search)));

            return Results.Json(response, statusCode: canConnect ? 200 : 503);
        })
        .WithName("GetHealth")
        .WithSummary("Status bazy i pozostałego limitu GitHub API");

        return app;
    }

    static RateLimitPoolHealth? Pool(RateLimitSnapshot? snapshot) =>
        snapshot is null ? null : new(snapshot.Remaining, snapshot.Limit, snapshot.Used, snapshot.ResetAt);
}
