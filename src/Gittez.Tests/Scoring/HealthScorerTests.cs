using Gittez.Core.Models;
using Gittez.Core.Scoring;

namespace Gittez.Tests.Scoring;

public class HealthScorerTests
{
    // Regresja na SPEC §6.2: przy garstce PR-ów odsetek merge'y nic nie mówi.
    [Fact]
    public void MergeRate_ponizej_pieciu_rozstrzygnietych_daje_null()
    {
        var pulls = Enumerable.Range(0, 4).Select(_ => Build.Pull(closedDaysAgo: 1, merged: true)).ToArray();

        var component = HealthScorer.MergeRate(pulls);

        Assert.Null(component.Points);
    }

    [Fact]
    public void MergeRate_liczy_odsetek_zmergowanych_wsrod_rozstrzygnietych()
    {
        var pulls = Enumerable.Range(0, 8).Select(_ => Build.Pull(closedDaysAgo: 1, merged: true))
            .Concat(Enumerable.Range(0, 2).Select(_ => Build.Pull(closedDaysAgo: 1)))
            .ToArray();

        var component = HealthScorer.MergeRate(pulls);

        Assert.Equal(20, component.Points!.Value, 6);
    }

    [Fact]
    public void MergeRate_pomija_drafty_i_boty()
    {
        var real = Enumerable.Range(0, 5).Select(_ => Build.Pull(closedDaysAgo: 1, merged: true));
        var noise = new[]
        {
            Build.Pull(closedDaysAgo: 1, draft: true),
            Build.Pull(closedDaysAgo: 1, login: "dependabot[bot]"),
            Build.Pull(closedDaysAgo: 1, login: "renovate", type: "Bot"),
        };

        var component = HealthScorer.MergeRate([.. real, .. noise]);

        Assert.Equal(HealthScorer.MergeRateMax, component.Points!.Value, 6);
        Assert.Equal("100 %", component.RawValue);
    }

    // Progi z pomiaru rozkładu (SPEC §0.5): p50 to 2-3 h, p75 to 17-24 h.
    [Theory]
    [InlineData(1, 25)]
    [InlineData(6, 19)]
    [InlineData(30, 13)]
    [InlineData(100, 7)]
    [InlineData(300, 2)]
    public void Latency_punktuje_wedlug_mediany_godzin(double hours, double expected)
    {
        var component = HealthScorer.ResolutionLatency([Build.ResolvedAfterHours(hours)]);

        Assert.Equal(expected, component.Points);
    }

    [Fact]
    public void Latency_bez_rozstrzygnietych_PR_ow_daje_null()
    {
        var component = HealthScorer.ResolutionLatency([Build.Pull(createdDaysAgo: 5)]);

        Assert.Null(component.Points);
    }

    // Regresja na SPEC §6.2: brak PR-ów nie jest dowodem zdrowia.
    [Fact]
    public void Stale_zero_otwartych_PR_ow_daje_null_a_nie_maksimum()
    {
        var component = HealthScorer.StaleRatio([], Build.Now);

        Assert.Null(component.Points);
    }

    [Fact]
    public void Stale_liczy_odsetek_PR_ow_starszych_niz_dziewiecdziesiat_dni()
    {
        var open = Enumerable.Range(0, 5).Select(_ => Build.Pull(createdDaysAgo: 120))
            .Concat(Enumerable.Range(0, 20).Select(_ => Build.Pull(createdDaysAgo: 10)))
            .ToArray();

        var component = HealthScorer.StaleRatio(open, Build.Now);

        Assert.Equal(16, component.Points!.Value, 6);
        Assert.False(component.IsSampled);
    }

    // Dokładnie setka oznacza, że per_page=100 uciął listę i próbka jest
    // z definicji najgorsza - najstarsze otwarte PR-y (SPEC §4.4 pkt 10).
    [Fact]
    public void Stale_dokladnie_sto_zwroconych_PR_ow_oznacza_probke()
    {
        var open = Enumerable.Range(0, 100).Select(_ => Build.Pull(createdDaysAgo: 10)).ToArray();

        var component = HealthScorer.StaleRatio(open, Build.Now);

        Assert.True(component.IsSampled);
        Assert.Contains("100 najstarszych", component.Explanation);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(20, 11)]
    [InlineData(60, 5)]
    [InlineData(200, 0)]
    public void Velocity_punktuje_wedlug_wieku_ostatniego_pusha(double daysAgo, double expected)
    {
        var component = HealthScorer.CommitVelocity(Build.Now.AddDays(-daysAgo), Build.Now);

        Assert.Equal(expected, component.Points);
    }

    // /issues zwraca też pull requesty - bez filtra turnaround jest zafałszowany.
    [Fact]
    public void Turnaround_odfiltrowuje_pull_requesty_z_listy_issues()
    {
        IReadOnlyList<ClosedIssue> closed =
        [
            Build.Issue(closedAfterDays: 200, isPullRequest: true),
            Build.Issue(closedAfterDays: 200, isPullRequest: true),
            Build.Issue(closedAfterDays: 200, isPullRequest: true),
            Build.Issue(closedAfterDays: 3),
        ];

        var component = HealthScorer.IssueTurnaround(closed);

        Assert.Equal(15, component.Points);
    }

    [Fact]
    public void Turnaround_bez_zamknietych_issues_daje_null()
    {
        var component = HealthScorer.IssueTurnaround([Build.Issue(closedAfterDays: 3, isPullRequest: true)]);

        Assert.Null(component.Points);
    }

    [Fact]
    public void Health_z_brakujacym_komponentem_procentuje_sie_po_dostepnych()
    {
        var input = Build.Health(
            // trzy rozstrzygnięte PR-y: za mało na Merge Rate, dość na latencję
            recentPulls: [.. Enumerable.Range(0, 3).Select(_ => Build.ResolvedAfterHours(1))],
            openPulls: [Build.Pull(createdDaysAgo: 10)],
            closedIssues: [Build.Issue(closedAfterDays: 3)],
            pushedDaysAgo: 1);

        var components = HealthScorer.Components(input, Build.Now);
        var score = HealthScorer.Score(input, Build.Now);

        Assert.Null(components.Single(c => c.Key == "merge_rate").Points);
        Assert.Equal(100, score!.Value, 6);
    }

    [Fact]
    public void Health_bez_zadnego_dostepnego_komponentu_daje_null()
    {
        var wszystkie_null = new[]
        {
            HealthScorer.MergeRate([]),
            HealthScorer.ResolutionLatency([]),
            HealthScorer.StaleRatio([], Build.Now),
            HealthScorer.IssueTurnaround([]),
        };

        Assert.Null(ScoreMath.Renormalize(wszystkie_null));
    }
}
