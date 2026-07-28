using GitBounty.Core.Models;
using GitBounty.Core.Profiles;

namespace GitBounty.Tests.Scoring;

public class ProfileBuilderTests
{
    static OwnedRepo Repo(string? language, int sizeKb = 100, IReadOnlyList<string>? topics = null, bool fork = false) =>
        new($"me/{language ?? "none"}-{sizeKb}", language, sizeKb, topics ?? [], fork);

    [Fact]
    public void Forki_nie_wchodza_do_profilu()
    {
        var profile = ProfileBuilder.Build("me",
        [
            Repo("C#", topics: ["blazor"]),
            Repo("Rust", topics: ["wasm"], fork: true),
        ]);

        Assert.Equal(1, profile.PublicRepoCount);
        Assert.Equal("C#", Assert.Single(profile.Languages).Name);
        Assert.Equal(["blazor"], profile.Interests);
    }

    [Fact]
    public void Ranking_sumuje_wlasne_repozytoria_i_kontrybucje()
    {
        var profile = ProfileBuilder.Build("me",
            [Repo("C#"), Repo("C#"), Repo("Swift")],
            new Dictionary<string, int> { ["Swift"] = 3, ["TypeScript"] = 1 });

        Assert.Equal(["Swift", "C#", "TypeScript"], profile.Languages.Select(l => l.Name));
        Assert.Equal(4, profile.Languages[0].Total);
    }

    // Bez tego repo z topikiem "csharp" punktowałoby dwa razy (SPEC §6.1).
    [Fact]
    public void Interests_nie_zawieraja_nazw_jezykow_uzytkownika()
    {
        var profile = ProfileBuilder.Build("me",
        [
            Repo("C#", topics: ["csharp", "c#", "blazor"]),
            Repo("C++", topics: ["cpp", "esp32"]),
        ]);

        Assert.Equal(["blazor", "esp32"], profile.Interests.Order());
    }

    [Fact]
    public void Mediana_rozmiaru_liczy_sie_z_nie_forkow()
    {
        var profile = ProfileBuilder.Build("me",
            [Repo("C#", 100), Repo("C#", 300), Repo("C#", 900), Repo("C#", 50_000, fork: true)]);

        Assert.Equal(300, profile.MedianSizeKb);
    }
}
