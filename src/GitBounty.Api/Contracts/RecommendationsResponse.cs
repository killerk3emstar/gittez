using GitBounty.Core.Models;

namespace GitBounty.Api.Contracts;

public sealed record RecommendationsResponse(
    IReadOnlyList<RecommendationItem> Items,
    // Brak wyników to poprawny wynik zapytania, nie błąd - stąd 200 z pustą
    // tablicą i podpowiedziami, co poluzować (SPEC §7.3).
    IReadOnlyList<string> Hints);

public sealed record RecommendationItem(
    string FullName,
    string? Description,
    string HtmlUrl,
    int Stars,
    string? PrimaryLanguage,
    IReadOnlyList<string> Topics,
    DateTimeOffset LastPushedAt,
    double MatchScore,
    double? HealthScore,
    // Służy wyłącznie do ustalenia kolejności, na karcie nie pojawia się jako
    // liczba: w widocznej dziesiątce wyniki mieszczą się w 6,8 punktu (SPEC §0.5).
    double FinalScore,
    IReadOnlyList<ScoreComponent> MatchBreakdown,
    IReadOnlyList<ScoreComponent> HealthBreakdown,
    IReadOnlyList<IssueResponse> Issues,
    DataFreshness DataFreshness);

public sealed record IssueResponse(
    int Number,
    string Title,
    string HtmlUrl,
    IReadOnlyList<string> Labels,
    int CommentCount,
    // szacunek heurystyczny, nie fakt (SPEC §6.3)
    int Difficulty,
    DateTimeOffset UpdatedAt);

public sealed record DataFreshness(DateTimeOffset Repo, DateTimeOffset? Health);
