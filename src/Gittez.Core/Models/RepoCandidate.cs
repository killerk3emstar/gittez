namespace Gittez.Core.Models;

// Komplet metadanych przychodzi z /search/repositories, więc kandydat nie
// wymaga ani jednego dodatkowego wywołania (SPEC §4.2).
public sealed record RepoCandidate(
    string FullName,
    string? Description,
    string HtmlUrl,
    int Stars,
    string? PrimaryLanguage,
    IReadOnlyList<string> Topics,
    // rozmiar repozytorium git w KB, nie LOC - proxy złożoności (SPEC §4.4 pkt 3)
    int SizeKb,
    DateTimeOffset PushedAt,
    string? License,
    bool Archived,
    bool Disabled,
    bool HasIssues,
    int OpenIssuesCount);
