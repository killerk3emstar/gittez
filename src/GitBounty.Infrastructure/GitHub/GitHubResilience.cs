using System.Net;
using Polly;

namespace GitBounty.Infrastructure.GitHub;

// GitHub ma wtórne limity kończące się 403 z Retry-After, którego nie widać
// w X-RateLimit-Remaining (SPEC §2.2). Ponawianie 403 z wyzerowanym licznikiem
// nie ma sensu - tam reset jest liczony w godzinach, nie sekundach.
static class GitHubResilience
{
    static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    public static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null) return outcome.Exception is HttpRequestException or TaskCanceledException;

        var response = outcome.Result;
        if (response is null) return false;

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return true;
        if ((int)response.StatusCode >= 500) return true;

        return response.StatusCode == HttpStatusCode.Forbidden && RetryAfter(outcome) is not null;
    }

    public static TimeSpan? RetryAfter(Outcome<HttpResponseMessage> outcome)
    {
        var retryAfter = outcome.Result?.Headers.RetryAfter;
        if (retryAfter is null) return null;

        var delay = retryAfter.Delta
            ?? (retryAfter.Date is { } date ? date - DateTimeOffset.UtcNow : null);

        return delay is { } d && d > TimeSpan.Zero
            ? d < MaxRetryDelay ? d : MaxRetryDelay
            : null;
    }
}
