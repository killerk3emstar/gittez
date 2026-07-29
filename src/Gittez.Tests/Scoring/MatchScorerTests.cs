using Gittez.Core.Models;
using Gittez.Core.Scoring;

namespace Gittez.Tests.Scoring;

public class MatchScorerTests
{
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 24)]
    [InlineData(2, 24)]
    [InlineData(3, 15)]
    [InlineData(4, 15)]
    [InlineData(5, 6)]
    public void Language_punktuje_wedlug_pozycji_w_rankingu(int rank, double expected)
    {
        var languages = Enumerable.Range(0, 6)
            .Select(i => new UserLanguage($"Lang{i}", 6 - i, 0))
            .ToArray();

        var component = MatchScorer.Language(Build.Repo(language: $"Lang{rank}"), Build.User(languages));

        Assert.Equal(expected, component.Points);
    }

    [Fact]
    public void Language_spoza_profilu_daje_zero()
    {
        var component = MatchScorer.Language(Build.Repo(language: "Rust"), Build.User());

        Assert.Equal(0, component.Points);
    }

    [Fact]
    public void Language_bez_jezyka_w_repo_daje_null()
    {
        var component = MatchScorer.Language(Build.Repo(language: null), Build.User());

        Assert.Null(component.Points);
    }

    // Merged PR do cudzego projektu mówi o umiejętnościach nie mniej niż własne
    // repo, więc język znany wyłącznie z kontrybucji musi wejść do rankingu.
    [Fact]
    public void Language_wylacznie_z_kontrybucji_liczy_sie_do_rankingu()
    {
        var user = Build.User([new UserLanguage("Swift", OwnedRepos: 0, ContributedRepos: 3)]);

        var component = MatchScorer.Language(Build.Repo(language: "Swift"), user);

        Assert.Equal(30, component.Points);
    }

    // Tryb awaryjny: bez tokenu i bez cache'u profil jest pusty, a zero
    // punktów byłoby karą za nasz brak danych.
    [Fact]
    public void Language_przy_pustym_profilu_daje_null()
    {
        var component = MatchScorer.Language(Build.Repo(language: "Swift"), Build.User(languages: []));

        Assert.Null(component.Points);
    }

    [Fact]
    public void Complexity_najmniejsze_repo_w_puli_dostaje_maksimum()
    {
        int[] pool = [100, 500, 1_000, 5_000];

        var component = MatchScorer.Complexity(Build.Repo(sizeKb: 100), pool);

        Assert.Equal(MatchScorer.ComplexityMax, component.Points);
    }

    [Fact]
    public void Complexity_najwieksze_repo_w_puli_dostaje_zero()
    {
        int[] pool = [100, 500, 1_000, 5_000];

        var component = MatchScorer.Complexity(Build.Repo(sizeKb: 5_000), pool);

        Assert.Equal(0, component.Points);
    }

    // Regresja: dzielnik to |pula| - 1, więc jednoelementowa pula musi zostać
    // obsłużona zanim dojdzie do dzielenia.
    [Fact]
    public void Complexity_jednoelementowa_pula_nie_dzieli_przez_zero()
    {
        var component = MatchScorer.Complexity(Build.Repo(sizeKb: 100), [100]);

        Assert.Null(component.Points);
    }

    [Fact]
    public void Complexity_identyczny_rozmiar_daje_identyczny_wynik()
    {
        int[] pool = [100, 500, 500, 5_000];

        var first = MatchScorer.Complexity(Build.Repo(sizeKb: 500), pool);
        var second = MatchScorer.Complexity(Build.Repo(sizeKb: 500), pool);

        Assert.Equal(first.Points, second.Points);
    }

    [Fact]
    public void Community_dokladnie_w_targecie_daje_maksimum()
    {
        var component = MatchScorer.Community(Build.Repo(stars: 500), targetStars: 500);

        Assert.Equal(MatchScorer.CommunityMax, component.Points!.Value, 6);
    }

    [Fact]
    public void Community_stukrotnosc_targetu_zbiega_do_zera()
    {
        var component = MatchScorer.Community(Build.Repo(stars: 50_000), targetStars: 500);

        Assert.True(component.Points < 0.1, $"oczekiwano wartości bliskiej zeru, było {component.Points}");
    }

    [Theory]
    [InlineData(1, 25.0 / 3)]
    [InlineData(2, 50.0 / 3)]
    [InlineData(3, 25)]
    [InlineData(4, 25)]
    public void Topic_punktuje_do_trzech_wspolnych_tematow(int overlap, double expected)
    {
        var topics = Enumerable.Range(0, overlap).Select(i => $"temat{i}").ToArray();
        var user = Build.User(interests: topics);

        var component = MatchScorer.Topic(Build.Repo(topics: topics), user);

        Assert.Equal(expected, component.Points!.Value, 6);
    }

    // Regresja na SPEC §6.1: brak topików to zaniedbanie metadanych, nie brak
    // dopasowania. Zero punktów przy wadze 25 kosztowałoby ćwiartkę wyniku.
    [Fact]
    public void Topic_puste_topics_repo_daje_null_a_nie_zero()
    {
        var component = MatchScorer.Topic(Build.Repo(topics: []), Build.User(interests: ["blazor"]));

        Assert.Null(component.Points);
    }

    [Fact]
    public void Topic_pusty_profil_zainteresowan_daje_null()
    {
        var component = MatchScorer.Topic(Build.Repo(topics: ["blazor"]), Build.User(interests: []));

        Assert.Null(component.Points);
    }

    // interests powstają bez nazw języków, inaczej repo z topikiem "csharp"
    // punktowałoby dwa razy: za język i za temat.
    [Fact]
    public void Topic_nazwa_jezyka_w_topikach_nie_daje_punktow_tematycznych()
    {
        var user = Build.User(
            languages: [new UserLanguage("C#", 7, 2)],
            interests: ["blazor", "esp32"]);

        var component = MatchScorer.Topic(Build.Repo(topics: ["csharp", "dotnet"]), user);

        Assert.Equal(0, component.Points);
    }

    [Fact]
    public void Match_z_kompletem_komponentow_daje_dokladnie_sto()
    {
        var user = Build.User(
            languages: [new UserLanguage("C#", 7, 2)],
            interests: ["blazor", "material-design", "ui"]);

        var repo = Build.Repo(
            language: "C#",
            stars: 500,
            sizeKb: 100,
            topics: ["blazor", "material-design", "ui"]);

        var score = MatchScorer.Score(repo, user, [100, 500, 1_000], targetStars: 500);

        Assert.Equal(100, score!.Value, 6);
    }

    // Regresja: bez renormalizacji repo bez topików traciłoby 25 punktów.
    [Fact]
    public void Match_bez_topikow_procentuje_sie_po_pozostalych_siedemdziesieciu_pieciu()
    {
        var user = Build.User(languages: [new UserLanguage("C#", 7, 2)], interests: ["blazor"]);
        var repo = Build.Repo(language: "C#", stars: 500, sizeKb: 100, topics: []);

        var components = MatchScorer.Components(repo, user, [100, 500, 1_000], targetStars: 500);
        var raw = components.Sum(c => c.Points ?? 0);

        Assert.Equal(75, raw, 6);
        Assert.Equal(100, ScoreMath.Renormalize(components)!.Value, 6);
        Assert.Null(components.Single(c => c.Key == "topic_match").Points);
    }
}
