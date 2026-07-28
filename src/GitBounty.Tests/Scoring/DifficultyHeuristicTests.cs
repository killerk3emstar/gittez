using GitBounty.Core.Scoring;

namespace GitBounty.Tests.Scoring;

public class DifficultyHeuristicTests
{
    [Theory]
    [InlineData("docs", 4_000, 20, 1)]
    [InlineData("area-documentation", 4_000, 20, 1)]
    [InlineData("bug", 300, 1, 1)]
    [InlineData("bug", 1_000, 15, 3)]
    [InlineData("bug", 4_000, 5, 3)]
    [InlineData("bug", 1_000, 5, 2)]
    public void Estimate_klasyfikuje_wylacznie_z_pol_listy_issues(
        string label, int bodyLength, int commentCount, int expected)
    {
        Assert.Equal(expected, DifficultyHeuristic.Estimate([label], bodyLength, commentCount));
    }
}
