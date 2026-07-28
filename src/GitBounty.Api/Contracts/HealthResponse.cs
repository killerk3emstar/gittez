namespace GitBounty.Api.Contracts;

public sealed record HealthResponse(
    string Status,
    DatabaseHealth Database,
    RateLimitHealth? RateLimit);

public sealed record DatabaseHealth(bool CanConnect, string? Error);

// Z nagłówków X-RateLimit-* realnych odpowiedzi GitHuba; endpoint /rate_limit
// raportuje nieaktualne dane (SPEC §4.4 pkt 12). Pule core i search mają
// osobne liczniki i osobne okna resetu.
public sealed record RateLimitHealth(RateLimitPoolHealth? Core, RateLimitPoolHealth? Search);

public sealed record RateLimitPoolHealth(int Remaining, int Limit, int Used, DateTimeOffset ResetAt);
