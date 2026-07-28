using GitBounty.Core.Models;
using GitBounty.Core.Recommendations;

namespace GitBounty.Core.Abstractions;

public interface IRepoCache
{
    Task<CachedProfile?> GetProfileAsync(string login, CancellationToken ct = default);
    Task SaveProfileAsync(UserProfile profile, CancellationToken ct = default);

    Task<CachedIssues?> GetIssuesAsync(string fullName, CancellationToken ct = default);
    Task SaveIssuesAsync(RepoCandidate repo, IReadOnlyList<ScoredIssue> issues, string? etag, CancellationToken ct = default);

    Task<CachedHealth?> GetHealthAsync(string fullName, CancellationToken ct = default);
    Task SaveHealthAsync(RepoCandidate repo, double? score, IReadOnlyList<ScoreComponent> breakdown, CancellationToken ct = default);

    // Awaryjne źródło kandydatów, gdy GitHub jest niedostępny: TTL nie
    // obowiązuje, bo stare dane są lepsze niż puste demo (SPEC §7.3).
    Task<IReadOnlyList<CachedCandidate>> GetCandidatesAsync(
        IReadOnlyList<string> languages, int starsLo, int starsHi, int limit, CancellationToken ct = default);
}

public static class CacheTtl
{
    public static readonly TimeSpan Profile = TimeSpan.FromHours(24);
    public static readonly TimeSpan RepoMetadata = TimeSpan.FromHours(6);
    public static readonly TimeSpan Issues = TimeSpan.FromHours(1);
    public static readonly TimeSpan Health = TimeSpan.FromHours(12);
}

public sealed record CachedProfile(UserProfile Profile, DateTimeOffset ComputedAt, bool IsFresh);

public sealed record CachedIssues(
    IReadOnlyList<ScoredIssue> Issues, string? ETag, DateTimeOffset FetchedAt, bool IsFresh);

public sealed record CachedHealth(
    double? Score, IReadOnlyList<ScoreComponent> Breakdown, DateTimeOffset ComputedAt, bool IsFresh);

public sealed record CachedCandidate(
    RepoCandidate Repo, CachedHealth? Health, IReadOnlyList<ScoredIssue> Issues);
