using GitBounty.Core.Abstractions;
using Microsoft.AspNetCore.Diagnostics;

namespace GitBounty.Api;

// SPEC §7.3: jednolity ProblemDetails. 503 dopiero wtedy, gdy nie mamy czym
// poratować - fallback na cache dokłada M5.
public sealed class GitHubExceptionHandler(ILogger<GitHubExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        switch (exception)
        {
            case GitHubUserNotFoundException notFound:
                await Results.Problem(
                    type: "github-user-not-found",
                    title: "Nie ma takiego użytkownika",
                    detail: notFound.Message,
                    statusCode: StatusCodes.Status404NotFound).ExecuteAsync(context);
                return true;

            case GitHubUnauthorizedException unauthorized:
                logger.LogError("Token GitHuba odrzucony, a cache nie miał czym poratować");

                await Results.Problem(
                    type: "github-unavailable",
                    title: "GitHub jest niedostępny",
                    detail: unauthorized.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
                return true;

            case GitHubRateLimitExceededException limited:
                logger.LogWarning("Limit GitHub API wyczerpany, reset {ResetAt}", limited.ResetAt);

                var retryAfter = (int)Math.Max(1, (limited.ResetAt - DateTimeOffset.UtcNow).TotalSeconds);
                context.Response.Headers.RetryAfter = retryAfter.ToString();

                await Results.Problem(
                    type: "github-rate-limited",
                    title: "Limit GitHub API wyczerpany",
                    detail: limited.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
                return true;

            default:
                return false;
        }
    }
}
