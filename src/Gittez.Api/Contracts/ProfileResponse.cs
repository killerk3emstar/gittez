namespace Gittez.Api.Contracts;

public sealed record ProfileResponse(
    string Login,
    int PublicRepoCount,
    int MedianSizeKb,
    IReadOnlyList<ProfileLanguageResponse> Languages,
    IReadOnlyList<string> Interests,
    DateTimeOffset ComputedAt);

public sealed record ProfileLanguageResponse(
    string Name,
    int OwnedRepos,
    int ContributedRepos,
    int Rank);
