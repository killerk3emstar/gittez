namespace Gittez.Core.Models;

public sealed record OwnedRepo(
    string FullName,
    string? Language,
    int SizeKb,
    IReadOnlyList<string> Topics,
    bool Fork);

public sealed record RateLimitSnapshot(
    int Remaining,
    int Limit,
    int Used,
    DateTimeOffset ResetAt,
    DateTimeOffset ObservedAt);
