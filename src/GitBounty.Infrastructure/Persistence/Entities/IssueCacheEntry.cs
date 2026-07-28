namespace GitBounty.Infrastructure.Persistence.Entities;

public class IssueCacheEntry
{
    // id nadane przez GitHuba, nie generowane po naszej stronie
    public long Id { get; set; }

    public required string RepoFullName { get; set; }
    public int Number { get; set; }
    public required string Title { get; set; }
    public required string HtmlUrl { get; set; }
    public List<string> Labels { get; set; } = [];
    public int CommentCount { get; set; }
    public int BodyLength { get; set; }

    // z tablicy assignees; pole assignee zniknęło z API w wersji 2026-03-10
    public bool HasAssignee { get; set; }

    public short Difficulty { get; set; }
    public DateTimeOffset IssueCreatedAt { get; set; }
    public DateTimeOffset IssueUpdatedAt { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}
