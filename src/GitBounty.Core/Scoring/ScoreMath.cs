using GitBounty.Core.Models;

namespace GitBounty.Core.Scoring;

public static class ScoreMath
{
    public const double MatchWeight = 0.65;
    public const double HealthWeight = 0.35;

    // Wspólne dla Match i Health: wynik procentuje się po komponentach, które
    // mają dane. Brak danych nie jest karą (SPEC §6.2).
    public static double? Renormalize(IEnumerable<ScoreComponent> components)
    {
        double points = 0;
        double max = 0;

        foreach (var c in components)
        {
            if (c.Points is not { } p) continue;
            points += p;
            max += c.MaxPoints;
        }

        return max <= 0 ? null : 100 * points / max;
    }

    public static double Final(double match, double? health) =>
        health is { } h ? MatchWeight * match + HealthWeight * h : match;

    // Pasmo log-symetryczne wokół preferencji użytkownika. Dolna granica jest
    // klamrowana do 100, bo poniżej tego progu w puli good-first-issues
    // wartościowych projektów praktycznie nie ma (SPEC §0.3).
    public static (int Lo, int Hi) StarBand(int targetStars)
    {
        var lo = Math.Max(100, targetStars / 5);
        var hi = Math.Max(lo, targetStars * 5);
        return (lo, hi);
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) throw new ArgumentException("pusta lista", nameof(values));

        var sorted = values.Order().ToArray();
        var mid = sorted.Length / 2;

        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
