namespace GitBounty.Infrastructure.Persistence.Entities;

public class ProfileEntity
{
    public required string GithubLogin { get; set; }
    public List<ProfileLanguage> TopLanguages { get; set; } = [];
    public int MedianSizeKb { get; set; }
    public List<string> Interests { get; set; } = [];
    public int PublicRepoCount { get; set; }
    public DateTimeOffset ComputedAt { get; set; }
}

public sealed record ProfileLanguage(
    string Name,
    int OwnedRepos,
    int ContributedRepos,
    double BytesShare);
