namespace Gittez.Core.Models;

public sealed record UserProfile(
    string Login,
    // posortowane malejąco, indeks 0 to język główny
    IReadOnlyList<UserLanguage> Languages,
    int MedianSizeKb,
    // topics z repozytoriów użytkownika, lowercase, BEZ nazw języków - inaczej
    // repo z topikiem "csharp" punktowałoby dwa razy (SPEC §6.1)
    IReadOnlyList<string> Interests,
    int PublicRepoCount);

// Kontrybucja waży tyle co własne repo, ranking liczy sumę obu źródeł.
public sealed record UserLanguage(string Name, int OwnedRepos, int ContributedRepos)
{
    public int Total => OwnedRepos + ContributedRepos;
}
