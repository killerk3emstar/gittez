using GitBounty.Core.Models;
using GitBounty.Core.Scoring;

namespace GitBounty.Tests.Scoring;

public class ScoreMathTests
{
    // Regresja na SPEC §0.4: bez pasma w zapytaniu cała pula ląduje w
    // przedziale 5k-55k gwiazdek i Community Fit jest stale zerowy.
    [Theory]
    [InlineData(500, 100, 2_500)]
    [InlineData(50, 100, 250)]
    [InlineData(5_000, 1_000, 25_000)]
    public void StarBand_jest_log_symetryczne_z_dolnym_progiem_sto(int target, int lo, int hi)
    {
        var band = ScoreMath.StarBand(target);

        Assert.Equal((lo, hi), band);
    }

    [Fact]
    public void Final_wazy_match_i_health_w_proporcji_65_35()
    {
        Assert.Equal(0.65 * 80 + 0.35 * 60, ScoreMath.Final(80, 60), 6);
    }

    [Fact]
    public void Final_bez_health_zwraca_sam_match()
    {
        Assert.Equal(80, ScoreMath.Final(80, null));
    }

    [Fact]
    public void RepoScorer_bez_policzonego_health_zwraca_null_i_final_rowny_match()
    {
        var user = Build.User(languages: [new UserLanguage("C#", 7, 2)], interests: ["blazor"]);
        var repo = Build.Repo(language: "C#", topics: ["blazor"], sizeKb: 100);

        var score = RepoScorer.Score(repo, user, [100, 500, 1_000], targetStars: 500, healthComponents: null);

        Assert.Null(score.HealthScore);
        Assert.Empty(score.HealthBreakdown);
        Assert.Equal(score.MatchScore, score.FinalScore);
    }

    [Theory]
    [InlineData(new double[] { 3 }, 3)]
    [InlineData(new double[] { 1, 3 }, 2)]
    [InlineData(new double[] { 5, 1, 3 }, 3)]
    [InlineData(new double[] { 4, 1, 3, 2 }, 2.5)]
    public void Median_liczy_srodek_takze_dla_parzystej_liczby_probek(double[] values, double expected)
    {
        Assert.Equal(expected, ScoreMath.Median(values), 6);
    }
}
