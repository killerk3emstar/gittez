using GitBounty.Api.Contracts;
using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;
using GitBounty.Core.Profiles;

namespace GitBounty.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/profile/{login}", async (
            string login,
            IGitHubClient github,
            CancellationToken ct) =>
        {
            var repos = await github.GetOwnedReposAsync(login, ct);
            var profile = ProfileBuilder.Build(login, repos);

            if (profile.PublicRepoCount == 0)
            {
                return Results.Problem(
                    type: "insufficient-profile-data",
                    title: "Za mało danych w profilu",
                    detail: $"Użytkownik {login} nie ma publicznych repozytoriów, z których dałoby się wyliczyć języki.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            return Results.Ok(ToResponse(profile));
        })
        .WithName("GetProfile")
        .WithSummary("Profil użytkownika z wykrytymi językami i tematami");

        return app;
    }

    static ProfileResponse ToResponse(UserProfile profile) => new(
        profile.Login,
        profile.PublicRepoCount,
        profile.MedianSizeKb,
        [.. profile.Languages.Select((l, i) => new ProfileLanguageResponse(l.Name, l.OwnedRepos, l.ContributedRepos, i + 1))],
        profile.Interests,
        DateTimeOffset.UtcNow);
}
