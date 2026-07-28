using GitBounty.Core.Models;

namespace GitBounty.Core.Scoring;

// Cztery komponenty, suma 100. Zero I/O - wszystko liczy się z metadanych,
// które przyszły z /search/repositories, więc Match dostają wszyscy kandydaci.
public static class MatchScorer
{
    public const double LanguageMax = 30;
    public const double TopicMax = 25;
    public const double ComplexityMax = 25;
    public const double CommunityMax = 20;

    public static IReadOnlyList<ScoreComponent> Components(
        RepoCandidate repo,
        UserProfile user,
        IReadOnlyList<int> poolSizes,
        int targetStars) =>
    [
        Language(repo, user),
        Topic(repo, user),
        Complexity(repo, poolSizes),
        Community(repo, targetStars),
    ];

    public static double? Score(
        RepoCandidate repo,
        UserProfile user,
        IReadOnlyList<int> poolSizes,
        int targetStars) =>
        ScoreMath.Renormalize(Components(repo, user, poolSizes, targetStars));

    public static ScoreComponent Language(RepoCandidate repo, UserProfile user)
    {
        const string key = "language_match";
        const string label = "Dopasowanie języka";

        if (string.IsNullOrWhiteSpace(repo.PrimaryLanguage))
        {
            return new(key, label, null, LanguageMax, "brak",
                "repozytorium nie ma ustawionego języka głównego");
        }

        // Pusty profil zdarza się w trybie awaryjnym, gdy GitHub jest
        // niedostępny i nie mamy z czego wykryć języków. Zero byłoby wtedy
        // karą za nasz brak danych, nie za niedopasowanie.
        if (user.Languages.Count == 0)
        {
            return new(key, label, null, LanguageMax, repo.PrimaryLanguage,
                "nie znamy Twoich języków, nie ma czego porównać");
        }

        var lang = repo.PrimaryLanguage;
        var rank = -1;
        for (var i = 0; i < user.Languages.Count; i++)
        {
            if (string.Equals(user.Languages[i].Name, lang, StringComparison.OrdinalIgnoreCase))
            {
                rank = i;
                break;
            }
        }

        if (rank < 0)
        {
            return new(key, label, 0, LanguageMax, lang,
                $"{lang} nie występuje w Twoim profilu");
        }

        double points = rank switch
        {
            0 => 30,
            1 or 2 => 24,
            3 or 4 => 15,
            _ => 6,
        };

        var source = user.Languages[rank];
        var position = rank == 0 ? "Twoim głównym językiem" : $"Twoim {rank + 1}. językiem";

        return new(key, label, points, LanguageMax, lang,
            $"{lang} jest {position} (własne repo: {source.OwnedRepos}, {Text.Contributions(source.ContributedRepos)})");
    }

    // Puste topics to null, nie zero: brak topików jest zaniedbaniem maintainera
    // w metadanych, a nie informacją o braku dopasowania (SPEC §6.1).
    public static ScoreComponent Topic(RepoCandidate repo, UserProfile user)
    {
        const string key = "topic_match";
        const string label = "Dopasowanie tematyczne";

        if (repo.Topics.Count == 0)
        {
            return new(key, label, null, TopicMax, "brak",
                "repozytorium nie ma uzupełnionych tematów");
        }

        if (user.Interests.Count == 0)
        {
            return new(key, label, null, TopicMax, string.Join(", ", repo.Topics),
                "Twoje repozytoria nie mają uzupełnionych tematów");
        }

        var interests = new HashSet<string>(user.Interests, StringComparer.OrdinalIgnoreCase);
        var shared = repo.Topics.Where(interests.Contains).ToArray();
        var points = TopicMax * Math.Min(shared.Length, 3) / 3.0;

        var explanation = shared.Length == 0
            ? "brak wspólnych tematów z Twoimi projektami"
            : $"wspólne tematy: {string.Join(", ", shared.Take(3))}";

        return new(key, label, points, TopicMax, string.Join(", ", repo.Topics), explanation);
    }

    // Percentyl w puli tego przebiegu, nie stosunek do mediany użytkownika:
    // ten drugi dawał minimum ponad połowie kandydatów (SPEC §0.5, §6.1).
    public static ScoreComponent Complexity(RepoCandidate repo, IReadOnlyList<int> poolSizes)
    {
        const string key = "complexity_match";
        const string label = "Przystępność rozmiaru";

        var raw = Text.Size(repo.SizeKb);

        if (poolSizes.Count < 2)
        {
            return new(key, label, null, ComplexityMax, raw,
                "za mało kandydatów, żeby porównać rozmiar");
        }

        var larger = poolSizes.Count(size => size > repo.SizeKb);
        var fraction = Math.Clamp((double)larger / (poolSizes.Count - 1), 0, 1);

        return new(key, label, ComplexityMax * fraction, ComplexityMax, raw,
            $"mniejsze niż {Text.Percent(fraction)} kandydatów, mniej kodu do ogarnięcia na start");
    }

    // Gaussian w skali logarytmicznej: maksimum w preferowanym rzędzie
    // wielkości, płynny spadek w obie strony.
    public static ScoreComponent Community(RepoCandidate repo, int targetStars)
    {
        const string key = "community_fit";
        const string label = "Wielkość społeczności";

        var d = Math.Log10(repo.Stars + 1) - Math.Log10(targetStars + 1);
        var points = CommunityMax * Math.Exp(-(d * d) / 0.5);

        var explanation = Math.Abs(d) switch
        {
            < 0.2 => $"{Text.Stars(repo.Stars)} gwiazdek, dokładnie w preferowanej skali projektu",
            < 0.6 => $"{Text.Stars(repo.Stars)} gwiazdek, blisko preferowanej skali projektu",
            _ when d > 0 => $"{Text.Stars(repo.Stars)} gwiazdek, znacznie więcej niż preferujesz - łatwiej zginąć w tłumie",
            _ => $"{Text.Stars(repo.Stars)} gwiazdek, znacznie mniej niż preferujesz - mniejsza szansa na mentoring",
        };

        return new(key, label, points, CommunityMax, Text.Stars(repo.Stars), explanation);
    }
}
