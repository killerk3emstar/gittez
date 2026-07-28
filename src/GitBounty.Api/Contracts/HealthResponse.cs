namespace GitBounty.Api.Contracts;

public sealed record HealthResponse(
    string Status,
    DatabaseHealth Database,
    RateLimitHealth? RateLimit);

public sealed record DatabaseHealth(bool CanConnect, string? Error);

// Wypełniane od M3 z nagłówków X-RateLimit-* realnych odpowiedzi GitHuba;
// endpoint /rate_limit raportuje nieaktualne dane (SPEC §4.4 pkt 12).
public sealed record RateLimitHealth(int Remaining, int Used, DateTimeOffset ResetAt);
