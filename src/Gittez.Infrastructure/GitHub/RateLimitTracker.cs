using System.Globalization;
using Gittez.Core.Abstractions;
using Gittez.Core.Models;

namespace Gittez.Infrastructure.GitHub;

// Nagłówki z realnych odpowiedzi, NIE endpoint /rate_limit: po 255 zużytych
// wywołaniach /rate_limit nadal raportował 5000/5000 (SPEC §4.4 pkt 12).
//
// Pule są rozdzielone, bo te same nagłówki niosą raz limit core (5000/h),
// a raz search (30/min) - trzymanie jednego snapshotu dawało w /api/health
// tę pulę, która akurat odpowiedziała ostatnia.
public sealed class RateLimitTracker : IRateLimitTracker
{
    RateLimitSnapshot? _core;
    RateLimitSnapshot? _search;

    public RateLimitSnapshot? Core => Volatile.Read(ref _core);
    public RateLimitSnapshot? Search => Volatile.Read(ref _search);

    public void Observe(HttpResponseMessage response, RateLimitPool pool)
    {
        var remaining = Header(response, "X-RateLimit-Remaining");
        var limit = Header(response, "X-RateLimit-Limit");
        var used = Header(response, "X-RateLimit-Used");
        var reset = Header(response, "X-RateLimit-Reset");

        if (remaining is null || reset is null) return;

        var snapshot = new RateLimitSnapshot(
            remaining.Value,
            limit ?? 0,
            used ?? 0,
            DateTimeOffset.FromUnixTimeSeconds(reset.Value),
            DateTimeOffset.UtcNow);

        if (pool == RateLimitPool.Search) Volatile.Write(ref _search, snapshot);
        else Volatile.Write(ref _core, snapshot);
    }

    static int? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
}

public enum RateLimitPool
{
    Core,
    Search,
}
