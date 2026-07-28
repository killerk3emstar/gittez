using System.Globalization;
using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;

namespace GitBounty.Infrastructure.GitHub;

// Nagłówki z realnych odpowiedzi, NIE endpoint /rate_limit: po 255 zużytych
// wywołaniach /rate_limit nadal raportował 5000/5000 (SPEC §4.4 pkt 12).
public sealed class RateLimitTracker : IRateLimitTracker
{
    RateLimitSnapshot? _current;

    public RateLimitSnapshot? Current => Volatile.Read(ref _current);

    public void Observe(HttpResponseMessage response)
    {
        var remaining = Header(response, "X-RateLimit-Remaining");
        var limit = Header(response, "X-RateLimit-Limit");
        var used = Header(response, "X-RateLimit-Used");
        var reset = Header(response, "X-RateLimit-Reset");

        if (remaining is null || reset is null) return;

        Volatile.Write(ref _current, new RateLimitSnapshot(
            remaining.Value,
            limit ?? 0,
            used ?? 0,
            DateTimeOffset.FromUnixTimeSeconds(reset.Value),
            DateTimeOffset.UtcNow));
    }

    static int? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
}
