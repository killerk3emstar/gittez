using Gittez.Core.Models;

namespace Gittez.Infrastructure.Persistence.Entities;

public class RepoCacheEntry
{
    public required string FullName { get; set; }

    // znormalizowany snapshot metadanych z /search/repositories
    public required RepoCandidate Data { get; set; }

    // ETag ostatniego wywołania per-finalista (/issues, /pulls), nie metadanych
    // repo - te przychodzą z search i nie są odpytywane warunkowo.
    public string? ETag { get; set; }

    public DateTimeOffset FetchedAt { get; set; }
    public decimal? HealthScore { get; set; }
    public List<ScoreComponent>? HealthBreakdown { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }
}
