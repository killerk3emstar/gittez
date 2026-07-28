namespace GitBounty.Api.Contracts;

public sealed record WatchlistItemResponse(
    long Id,
    string RepoFullName,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // Metadane dokładane z cache'u, gdy repo tam jest - watchlista nie wydaje
    // ani jednego wywołania do GitHuba, więc działa też po wyczerpaniu limitu.
    WatchlistRepoResponse? Repo);

public sealed record WatchlistRepoResponse(
    string? Description,
    string HtmlUrl,
    int Stars,
    string? PrimaryLanguage,
    DateTimeOffset LastPushedAt,
    double? HealthScore);

public sealed record CreateWatchlistItemRequest(string RepoFullName, string? Note);

public sealed record UpdateWatchlistItemRequest(string? Note);
