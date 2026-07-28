using System.Globalization;

namespace GitBounty.Core.Scoring;

// Opisy komponentów powstają razem z liczbą, więc formatowanie siedzi tutaj,
// a nie w UI (SPEC §6). Kultura jest wymuszona, bo kontener startuje na
// kulturze invariant i przecinek dziesiętny by zniknął.
internal static class Text
{
    internal static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");

    internal static string Number(double value, string format = "0.#") =>
        value.ToString(format, Pl);

    internal static string Percent(double fraction) =>
        $"{Math.Round(fraction * 100).ToString("0", Pl)} %";

    internal static string Stars(int stars) =>
        stars.ToString("N0", Pl);

    internal static string Contributions(int n) => n switch
    {
        1 => "1 kontrybucja",
        _ when n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 => $"{n} kontrybucje",
        _ => $"{n} kontrybucji",
    };

    // size z API to rozmiar repozytorium git w KB
    internal static string Size(int sizeKb) => sizeKb >= 1024
        ? $"{Number(sizeKb / 1024.0)} MB"
        : $"{sizeKb} KB";

    internal static string Hours(double hours) => hours >= 48
        ? Days(hours / 24)
        : $"{Number(hours)} h";

    internal static string Days(double days)
    {
        var whole = (int)Math.Round(days);
        if (whole == 0) return $"{Number(days * 24)} h";
        return whole == 1 ? "1 dzień" : $"{whole} dni";
    }
}
