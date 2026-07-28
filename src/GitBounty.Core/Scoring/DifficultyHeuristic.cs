using GitBounty.Core.Models;

namespace GitBounty.Core.Scoring;

// Bez zgadywania LOC - wyłącznie z pól, które lista issues już zwróciła.
// W UI opisane jako szacunek heurystyczny, nie jako fakt (SPEC §6.3).
public static class DifficultyHeuristic
{
    static readonly string[] EasyLabels =
        ["docs", "documentation", "typo", "readme", "translation"];

    public static int Estimate(IssueSummary issue) => Estimate(issue.Labels, issue.BodyLength, issue.CommentCount);

    public static int Estimate(IReadOnlyList<string> labels, int bodyLength, int commentCount)
    {
        var easyLabel = labels.Any(l => EasyLabels.Any(e => l.Contains(e, StringComparison.OrdinalIgnoreCase)));

        if (easyLabel || (bodyLength < 500 && commentCount <= 2)) return 1;
        if (commentCount > 10 || bodyLength > 3000) return 3;
        return 2;
    }
}
