using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;

namespace GitBounty.Core.Profiles;

// ComputedAt to znacznik z cache'u, nie czas odpowiedzi - inaczej banner
// „dane sprzed X godzin" zawsze pokazywałby zero.
public sealed record ProfileResult(UserProfile Profile, bool IsStale, DateTimeOffset ComputedAt);

public sealed class ProfileProvider(IGitHubClient github, IRepoCache cache, TimeProvider time)
{
    // Rzuca GitHubUnavailableException dopiero wtedy, gdy nie ma ani świeżych
    // danych, ani niczego w cache'u - wtedy API odpowiada 503 (SPEC §7.3).
    public async Task<ProfileResult> GetAsync(string login, CancellationToken ct = default)
    {
        var cached = await cache.GetProfileAsync(login, ct);
        if (cached is { IsFresh: true }) return new ProfileResult(cached.Profile, false, cached.ComputedAt);

        try
        {
            var profile = ProfileBuilder.Build(login, await github.GetOwnedReposAsync(login, ct));
            await cache.SaveProfileAsync(profile, ct);
            return new ProfileResult(profile, false, time.GetUtcNow());
        }
        catch (GitHubUnavailableException)
        {
            if (cached is null) throw;
            return new ProfileResult(cached.Profile, true, cached.ComputedAt);
        }
    }
}
