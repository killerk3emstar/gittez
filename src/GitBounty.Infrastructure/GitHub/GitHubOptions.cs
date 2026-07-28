namespace GitBounty.Infrastructure.GitHub;

public sealed class GitHubOptions
{
    public const string Section = "GitHub";

    public string? Token { get; set; }
    public string BaseAddress { get; set; } = "https://api.github.com/";
    public string UserAgent { get; set; } = "GitBounty";

    // Kandydaci muszą być ruszani; 90 dni to filtr po stronie GitHuba (SPEC §5).
    public int MaxPushedAgeDays { get; set; } = 90;
}
