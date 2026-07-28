using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;

namespace GitBounty.Core.Profiles;

public sealed class ProfileProvider(IGitHubClient github, IRepoCache cache)
{
    // Rzuca GitHubUnavailableException dopiero wtedy, gdy nie ma ani świeżych
    // danych, ani niczego w cache'u - wtedy API odpowiada 503 (SPEC §7.3).
    public async Task<(UserProfile Profile, bool IsStale)> GetAsync(string login, CancellationToken ct = default)
    {
        var cached = await cache.GetProfileAsync(login, ct);
        if (cached is { IsFresh: true }) return (cached.Profile, false);

        try
        {
            var profile = ProfileBuilder.Build(login, await github.GetOwnedReposAsync(login, ct));
            await cache.SaveProfileAsync(profile, ct);
            return (profile, false);
        }
        catch (GitHubUnavailableException)
        {
            if (cached is null) throw;
            return (cached.Profile, true);
        }
    }
}
