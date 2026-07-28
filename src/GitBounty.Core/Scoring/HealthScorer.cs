using GitBounty.Core.Models;

namespace GitBounty.Core.Scoring;

// Pięć komponentów, suma 100, trzy wywołania na repozytorium - Commit Velocity
// liczy się z pushed_at, które przyszło już z search (SPEC §6.2).
public static class HealthScorer
{
    public const double MergeRateMax = 25;
    public const double LatencyMax = 25;
    public const double StaleMax = 20;
    public const double VelocityMax = 15;
    public const double TurnaroundMax = 15;

    const int MergeRateMinSample = 5;
    const int StaleAfterDays = 90;

    // per_page=100 z najstarszymi PR-ami: dokładnie setka oznacza, że lista
    // została ucięta i ratio jest próbką (SPEC §4.4 pkt 10)
    const int OpenPullsPageSize = 100;

    public static IReadOnlyList<ScoreComponent> Components(HealthInput input, DateTimeOffset now) =>
    [
        MergeRate(input.RecentPulls),
        ResolutionLatency(input.RecentPulls),
        StaleRatio(input.OldestOpenPulls, now),
        CommitVelocity(input.PushedAt, now),
        IssueTurnaround(input.ClosedIssues),
    ];

    public static double? Score(HealthInput input, DateTimeOffset now) =>
        ScoreMath.Renormalize(Components(input, now));

    public static ScoreComponent MergeRate(IReadOnlyList<PullSummary> recentPulls)
    {
        const string key = "merge_rate";
        const string label = "Odsetek zmergowanych PR-ów";

        var resolved = recentPulls.Where(IsRelevant).Where(p => p.ClosedAt is not null).ToArray();

        if (resolved.Length < MergeRateMinSample)
        {
            return new(key, label, null, MergeRateMax, $"{resolved.Length} PR-ów",
                $"za mało rozstrzygniętych PR-ów, żeby to policzyć ({resolved.Length} z wymaganych {MergeRateMinSample})");
        }

        var merged = resolved.Count(p => p.MergedAt is not null);
        var rate = (double)merged / resolved.Length;

        return new(key, label, MergeRateMax * rate, MergeRateMax, Text.Percent(rate),
            $"{Text.Percent(rate)} rozstrzygniętych PR-ów zostało zmergowanych ({merged} z {resolved.Length})");
    }

    // Progi skalibrowane na zmierzonym rozkładzie (SPEC §0.5): p50 to 2-3 h,
    // więc stare "≤ 48 h → komplet" dawało maksimum niemal wszystkim.
    public static ScoreComponent ResolutionLatency(IReadOnlyList<PullSummary> recentPulls)
    {
        const string key = "resolution_latency";
        const string label = "Czas rozstrzygania PR-ów";

        var resolved = recentPulls.Where(IsRelevant).Where(p => p.ClosedAt is not null).ToArray();

        if (resolved.Length == 0)
        {
            return new(key, label, null, LatencyMax, "brak danych",
                "żaden z pobranych PR-ów nie został jeszcze rozstrzygnięty");
        }

        var hours = ScoreMath.Median(
            [.. resolved.Select(p => ((p.MergedAt ?? p.ClosedAt)!.Value - p.CreatedAt).TotalHours)]);

        double points = hours switch
        {
            <= 2 => 25,
            <= 12 => 19,
            <= 48 => 13,
            <= 24 * 7 => 7,
            _ => 2,
        };

        return new(key, label, points, LatencyMax, Text.Hours(hours),
            $"mediana czasu od zgłoszenia do zamknięcia PR-a: {Text.Hours(hours)}");
    }

    // Zero otwartych PR-ów daje null, nie maksimum: brak PR-ów częściej jest
    // dowodem, że nikt nic nie zgłasza, niż dowodem zdrowia (SPEC §6.2).
    public static ScoreComponent StaleRatio(IReadOnlyList<PullSummary> oldestOpenPulls, DateTimeOffset now)
    {
        const string key = "stale_ratio";
        const string label = "Zaniedbane PR-y";

        var sampled = oldestOpenPulls.Count >= OpenPullsPageSize;
        var open = oldestOpenPulls.Where(IsRelevant).ToArray();

        if (open.Length == 0)
        {
            return new(key, label, null, StaleMax, "brak otwartych PR-ów",
                "repozytorium nie ma otwartych PR-ów, nie ma czego mierzyć");
        }

        var cutoff = now.AddDays(-StaleAfterDays);
        var stale = open.Count(p => p.CreatedAt < cutoff);
        var ratio = (double)stale / open.Length;

        var scope = sampled ? " spośród 100 najstarszych" : "";
        var explanation =
            $"{Text.Percent(ratio)} otwartych PR-ów{scope} czeka ponad {StaleAfterDays} dni ({stale} z {open.Length})";

        return new(key, label, StaleMax * (1 - ratio), StaleMax, Text.Percent(ratio), explanation, sampled);
    }

    public static ScoreComponent CommitVelocity(DateTimeOffset pushedAt, DateTimeOffset now)
    {
        const string key = "commit_velocity";
        const string label = "Tempo commitów";

        var days = Math.Max(0, (now - pushedAt).TotalDays);

        double points = days switch
        {
            <= 7 => 15,
            <= 30 => 11,
            <= 90 => 5,
            _ => 0,
        };

        return new(key, label, points, VelocityMax, Text.Days(days),
            $"ostatni push {Text.Days(days)} temu");
    }

    public static ScoreComponent IssueTurnaround(IReadOnlyList<ClosedIssue> closedIssues)
    {
        const string key = "issue_turnaround";
        const string label = "Czas zamykania issues";

        var issues = closedIssues
            .Where(i => !i.IsPullRequest)
            .Where(i => i.ClosedAt is not null)
            .ToArray();

        if (issues.Length == 0)
        {
            return new(key, label, null, TurnaroundMax, "brak danych",
                "brak zamkniętych issues do zmierzenia");
        }

        var days = ScoreMath.Median(
            [.. issues.Select(i => (i.ClosedAt!.Value - i.CreatedAt).TotalDays)]);

        double points = days switch
        {
            <= 7 => 15,
            <= 30 => 11,
            <= 90 => 6,
            _ => 2,
        };

        return new(key, label, points, TurnaroundMax, Text.Days(days),
            $"mediana czasu zamknięcia issue: {Text.Days(days)}");
    }

    static bool IsRelevant(PullSummary pull) => !pull.IsDraft && !IsBot(pull);

    static bool IsBot(PullSummary pull) =>
        string.Equals(pull.AuthorType, "Bot", StringComparison.OrdinalIgnoreCase)
        || pull.AuthorLogin.EndsWith("[bot]", StringComparison.OrdinalIgnoreCase);
}
