using System.Text.Json.Serialization;

namespace GitBounty.Infrastructure.GitHub;

// Kształty zweryfikowane na żywym API w tools/calibrate.py. Nazwy pól mapują
// się przez JsonNamingPolicy.SnakeCaseLower, więc nie ma tu atrybutów.
sealed record RepoDto(
    string FullName,
    string? Description,
    string HtmlUrl,
    int StargazersCount,
    string? Language,
    List<string>? Topics,
    int Size,
    DateTimeOffset PushedAt,
    LicenseDto? License,
    bool Archived,
    bool Disabled,
    bool HasIssues,
    int OpenIssuesCount,
    bool Fork);

sealed record LicenseDto(string? SpdxId, string? Name);

sealed record SearchResponseDto(int TotalCount, bool IncompleteResults, List<RepoDto> Items);

sealed record IssueDto(
    long Id,
    int Number,
    string Title,
    string HtmlUrl,
    List<LabelDto>? Labels,
    int Comments,
    string? Body,
    // pole assignee (liczba pojedyncza) zniknęło z API w wersji 2026-03-10
    List<UserDto>? Assignees,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    // obecne wyłącznie wtedy, gdy element listy issues jest pull requestem
    [property: JsonPropertyName("pull_request")] JsonPullRequestMarker? PullRequest);

sealed record JsonPullRequestMarker(string? Url);

sealed record LabelDto(string Name);

sealed record UserDto(string Login, string Type);

sealed record PullDto(
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? MergedAt,
    bool Draft,
    UserDto? User);
