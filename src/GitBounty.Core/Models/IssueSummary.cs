namespace GitBounty.Core.Models;

public sealed record IssueSummary(
    long Id,
    int Number,
    string Title,
    string HtmlUrl,
    IReadOnlyList<string> Labels,
    int CommentCount,
    int BodyLength,
    // z tablicy assignees; pole assignee zniknęło z API w wersji 2026-03-10
    bool HasAssignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
