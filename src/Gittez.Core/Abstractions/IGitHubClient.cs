using Gittez.Core.Models;

namespace Gittez.Core.Abstractions;

public interface IGitHubClient
{
    Task<IReadOnlyList<OwnedRepo>> GetOwnedReposAsync(string login, CancellationToken ct = default);

    Task<IReadOnlyList<RepoCandidate>> SearchRepositoriesAsync(
        string language, int starsLo, int starsHi, CancellationToken ct = default);

    // ETag ma sens tylko na wywołaniach per-finalista - to one powtarzają się
    // między przebiegami i zjadają 95 % puli core (SPEC §4.4 pkt 9).
    Task<GitHubResult<IReadOnlyList<IssueSummary>>> GetGoodFirstIssuesAsync(
        string fullName, string? etag = null, CancellationToken ct = default);

    Task<HealthInput> GetHealthInputAsync(
        string fullName, DateTimeOffset pushedAt, CancellationToken ct = default);
}

public sealed record GitHubResult<T>(T? Value, string? ETag, bool NotModified);

public interface IRateLimitTracker
{
    // Trzy niezależne pule limitów (SPEC §4.1); core to 96 % budżetu przebiegu.
    RateLimitSnapshot? Core { get; }
    RateLimitSnapshot? Search { get; }
}

public sealed class GitHubUserNotFoundException(string login)
    : Exception($"Użytkownik {login} nie istnieje na GitHubie")
{
    public string Login { get; } = login;
}

// Wspólna nadklasa dla sytuacji, w których GitHub przestaje odpowiadać na
// świeże dane, a my przechodzimy na cache zamiast zwracać błąd.
public abstract class GitHubUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed class GitHubRateLimitExceededException(DateTimeOffset resetAt)
    : GitHubUnavailableException($"Limit GitHub API wyczerpany, reset o {resetAt:HH:mm:ss}")
{
    public DateTimeOffset ResetAt { get; } = resetAt;
}

public sealed class GitHubUnauthorizedException()
    : GitHubUnavailableException("Token GitHuba jest nieprawidłowy lub wygasł");

// Zerwane połączenie, timeout albo 5xx, które przeżyło ponowienia. Dla nas
// znaczy to samo co odrzucony token: świeżych danych nie będzie, zostaje cache.
public sealed class GitHubTransportException(string message, Exception? inner = null)
    : GitHubUnavailableException(message, inner);

// 404 na konkretnym zasobie. To nie jest niedostępność GitHuba: repozytorium
// mogło zniknąć albo zostać uprywatnione między wyszukaniem a pobraniem issues,
// więc wypada z listy zamiast wywracać przebieg.
public sealed class GitHubNotFoundException(string path)
    : Exception($"GitHub zwrócił 404 dla {path}");
