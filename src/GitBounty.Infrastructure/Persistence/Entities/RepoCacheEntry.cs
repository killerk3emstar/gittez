using GitBounty.Core.Models;

namespace GitBounty.Infrastructure.Persistence.Entities;

public class RepoCacheEntry
{
    public required string FullName { get; set; }
    public required RepoSnapshot Data { get; set; }

    // ETag ostatniego wywołania per-finalista (/issues, /pulls), nie metadanych
    // repo - te przychodzą z /search/repositories i nie są odpytywane warunkowo.
    public string? ETag { get; set; }

    public DateTimeOffset FetchedAt { get; set; }
    public decimal? HealthScore { get; set; }
    public List<ScoreComponent>? HealthBreakdown { get; set; }
    public DateTimeOffset? HealthComputedAt { get; set; }
}

// Znormalizowany snapshot obiektu repozytorium z /search/repositories.
public sealed record RepoSnapshot(
    string FullName,
    string? Description,
    string HtmlUrl,
    int Stars,
    string? PrimaryLanguage,
    IReadOnlyList<string> Topics,
    int SizeKb,
    DateTimeOffset PushedAt,
    string? License,
    bool Archived,
    bool Disabled,
    bool HasIssues,
    int OpenIssuesCount);
