using Gittez.Core.Models;

namespace Gittez.Core.Profiles;

// Profil powstaje z publicznych repozytoriów użytkownika; kontrybucje są
// opcjonalnym drugim źródłem (GraphQL, SPEC §5 krok 1b).
public static class ProfileBuilder
{
    public static UserProfile Build(
        string login,
        IReadOnlyList<OwnedRepo> repos,
        IReadOnlyDictionary<string, int>? contributedByLanguage = null)
    {
        var owned = repos.Where(r => !r.Fork).ToArray();

        var ownedByLanguage = owned
            .Where(r => !string.IsNullOrWhiteSpace(r.Language))
            .GroupBy(r => r.Language!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var contributed = contributedByLanguage is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(contributedByLanguage, StringComparer.OrdinalIgnoreCase);

        // Kontrybucja waży tyle co własne repo (SPEC §6.1).
        var languages = ownedByLanguage.Keys
            .Union(contributed.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(name => new UserLanguage(
                name,
                ownedByLanguage.GetValueOrDefault(name),
                contributed.GetValueOrDefault(name)))
            .OrderByDescending(l => l.Total)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var languageAliases = languages
            .SelectMany(l => Aliases(l.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Nazwy języków wypadają z interests, inaczej repo z topikiem "csharp"
        // punktowałoby dwa razy: za język i za temat (SPEC §6.1).
        var interests = owned
            .SelectMany(r => r.Topics)
            .Select(t => t.ToLowerInvariant())
            .Where(t => !languageAliases.Contains(t))
            .Distinct()
            .ToArray();

        var sizes = owned.Select(r => r.SizeKb).Order().ToArray();
        var medianSizeKb = sizes.Length == 0
            ? 0
            : sizes.Length % 2 == 1
                ? sizes[sizes.Length / 2]
                : (sizes[sizes.Length / 2 - 1] + sizes[sizes.Length / 2]) / 2;

        return new UserProfile(login, languages, medianSizeKb, interests, owned.Length);
    }

    // "C#" w topikach żyje jako "csharp", "C++" jako "cpp"
    static IEnumerable<string> Aliases(string language)
    {
        var lower = language.ToLowerInvariant();
        yield return lower;
        yield return lower.Replace("#", "sharp").Replace("+", "p").Replace(' ', '-');
    }
}
