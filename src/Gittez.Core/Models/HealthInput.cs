namespace Gittez.Core.Models;

// Trzy wywołania na repozytorium plus pushed_at, które przyszło już z search.
public sealed record HealthInput(
    // /pulls?state=all&per_page=30&sort=updated
    IReadOnlyList<PullSummary> RecentPulls,
    // /pulls?state=open&sort=created&direction=asc&per_page=100
    IReadOnlyList<PullSummary> OldestOpenPulls,
    // /issues?state=closed&per_page=30
    IReadOnlyList<ClosedIssue> ClosedIssues,
    DateTimeOffset PushedAt);

public sealed record PullSummary(
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? MergedAt,
    bool IsDraft,
    string AuthorLogin,
    string AuthorType);

public sealed record ClosedIssue(
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    // /issues zwraca też pull requesty (SPEC §4.4 pkt 5)
    bool IsPullRequest);
