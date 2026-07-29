using Gittez.Core.Abstractions;
using Gittez.Core.Models;
using Gittez.Core.Profiles;
using Gittez.Core.Recommendations;
using Gittez.Tests.Scoring;

namespace Gittez.Tests.Recommendations;

// Awarie po stronie GitHuba: co wypada z listy, co schodzi na cache, a co
// przechodzi dalej jako błąd.
public class RecommendationPipelineTests
{
    static readonly FakeTime Time = new(Build.Now);

    [Fact]
    public async Task Repozytorium_ktore_zniknelo_wypada_z_listy_zamiast_wywracac_przebieg()
    {
        var github = new FakeGitHub();
        github.Found.AddRange([Repo("owner/znikniete"), Repo("owner/zywe")]);

        github.Issues = fullName => fullName == "owner/znikniete"
            ? throw new GitHubNotFoundException($"repos/{fullName}/issues")
            : [Issue()];

        var result = await Run(github, new FakeCache { Profile = Build.User() }, Request());

        Assert.Equal(["owner/zywe"], result.Items.Select(i => i.Repo.FullName));
        Assert.False(result.IsStale);
    }

    [Fact]
    public async Task Wyczerpany_limit_przy_pobieraniu_issues_schodzi_na_cache()
    {
        var github = new FakeGitHub();
        github.Found.Add(Repo("owner/finalista"));
        github.Issues = _ => throw new GitHubRateLimitExceededException(Build.Now.AddMinutes(30));

        var cache = new FakeCache { Profile = Build.User() };
        cache.Candidates.Add(Cached("owner/z-cachu", difficulty: 1));

        var result = await Run(github, cache, Request());

        Assert.True(result.IsStale);
        Assert.Equal(["owner/z-cachu"], result.Items.Select(i => i.Repo.FullName));
    }

    [Fact]
    public async Task Zerwane_polaczenie_przy_wyszukiwaniu_schodzi_na_cache()
    {
        var github = new FakeGitHub
        {
            SearchFailure = new GitHubTransportException("GitHub nie odpowiedział na search/repositories"),
        };

        var cache = new FakeCache { Profile = Build.User() };
        cache.Candidates.Add(Cached("owner/z-cachu", difficulty: 1));

        var result = await Run(github, cache, Request());

        Assert.True(result.IsStale);
        Assert.Equal(["owner/z-cachu"], result.Items.Select(i => i.Repo.FullName));
    }

    [Fact]
    public async Task Tryb_awaryjny_respektuje_maksymalna_trudnosc()
    {
        var github = new FakeGitHub
        {
            SearchFailure = new GitHubTransportException("GitHub nie odpowiedział na search/repositories"),
        };

        var cache = new FakeCache { Profile = Build.User() };
        cache.Candidates.Add(Cached("owner/trudne", difficulty: 3));

        var result = await Run(github, cache, Request(maxDifficulty: 1));

        Assert.Empty(result.Items);
        Assert.Contains(result.Hints, h => h.Contains("cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tryb_awaryjny_zostawia_issues_miesczace_sie_w_trudnosci()
    {
        var github = new FakeGitHub
        {
            SearchFailure = new GitHubTransportException("GitHub nie odpowiedział na search/repositories"),
        };

        var cache = new FakeCache { Profile = Build.User() };
        cache.Candidates.Add(Cached("owner/mieszane", difficulty: 3, extraDifficulty: 1));

        var result = await Run(github, cache, Request(maxDifficulty: 1));

        var item = Assert.Single(result.Items);
        Assert.All(item.Issues, i => Assert.Equal(1, i.Difficulty));
    }

    [Fact]
    public async Task Brak_swiezych_danych_i_pustego_cachu_konczy_sie_bledem()
    {
        var github = new FakeGitHub
        {
            SearchFailure = new GitHubUnauthorizedException(),
        };

        var result = await Run(github, new FakeCache { Profile = Build.User() }, Request());

        Assert.Empty(result.Items);
        Assert.NotEmpty(result.Hints);
    }

    static async Task<RecommendationResult> Run(FakeGitHub github, FakeCache cache, RecommendationRequest request)
    {
        var profiles = new ProfileProvider(github, cache, Time);
        var pipeline = new RecommendationPipeline(github, cache, profiles, Time);

        return await pipeline.RunAsync(request);
    }

    static RecommendationRequest Request(int? maxDifficulty = null) =>
        new("killerk3emstar", ["C#"], TargetStars: 500, MaxDifficulty: maxDifficulty);

    static RepoCandidate Repo(string fullName) => Build.Repo() with { FullName = fullName };

    static IssueSummary Issue(bool hasAssignee = false) => new(
        Id: 1,
        Number: 1,
        Title: "Poprawić literówkę",
        HtmlUrl: "https://github.com/owner/name/issues/1",
        Labels: ["good first issue"],
        CommentCount: 0,
        BodyLength: 120,
        HasAssignee: hasAssignee,
        CreatedAt: Build.Now.AddDays(-10),
        UpdatedAt: Build.Now.AddDays(-1));

    static CachedCandidate Cached(string fullName, int difficulty, int? extraDifficulty = null)
    {
        List<ScoredIssue> issues = [new(Issue(), difficulty)];

        if (extraDifficulty is { } extra) issues.Add(new(Issue() with { Id = 2, Number = 2 }, extra));

        return new CachedCandidate(Repo(fullName), Health: null, issues, Build.Now.AddHours(-9));
    }
}
