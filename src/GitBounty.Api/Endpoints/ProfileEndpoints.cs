using GitBounty.Api.Contracts;
using GitBounty.Core.Models;
using GitBounty.Core.Profiles;

namespace GitBounty.Api.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/profile/{login}", async (
            string login,
            ProfileProvider profiles,
            HttpContext http,
            CancellationToken ct) =>
        {
            var (profile, isStale, computedAt) = await profiles.GetAsync(login, ct);

            if (profile.PublicRepoCount == 0)
            {
                return Results.Problem(
                    type: "insufficient-profile-data",
                    title: "Za mało danych w profilu",
                    detail: $"Użytkownik {login} nie ma publicznych repozytoriów, z których dałoby się wyliczyć języki.",
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (isStale) http.Response.Headers["X-Data-Stale"] = "true";

            return Results.Ok(ToResponse(profile, computedAt));
        })
        .WithName("GetProfile")
        .WithSummary("Profil użytkownika z wykrytymi językami i tematami");

        return app;
    }

    static ProfileResponse ToResponse(UserProfile profile, DateTimeOffset computedAt) => new(
        profile.Login,
        profile.PublicRepoCount,
        profile.MedianSizeKb,
        [.. profile.Languages.Select((l, i) => new ProfileLanguageResponse(l.Name, l.OwnedRepos, l.ContributedRepos, i + 1))],
        profile.Interests,
        computedAt);
}
