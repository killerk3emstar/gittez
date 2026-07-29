using System.Net;
using System.Net.Http.Json;
using Gittez.Api.Contracts;
using Gittez.Core.Models;
using Gittez.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gittez.Tests.Integration;

// Jedyny test dotykający HTTP i bazy: dowodzi, że ścieżka zapisu działa
// end-to-end, razem z leniwym tworzeniem sesji (SPEC §10).
public class WatchlistFlowTests(GittezApp app) : IClassFixture<GittezApp>
{
    const string Repo = "MudBlazor/MudBlazor";

    [Fact]
    public async Task Zapis_edycja_i_usuniecie_przechodza_przez_api()
    {
        var client = app.CreateClientWithSchema();
        var session = Guid.NewGuid();

        await app.WithDatabaseAsync(async db =>
        {
            if (!await db.RepoCache.AnyAsync(r => r.FullName == Repo)) db.RepoCache.Add(CachedRepo());
        });

        var created = await client.SendAsync(Post(session, new CreateWatchlistItemRequest(Repo, "sprawdzić issue #42")));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var item = await created.Content.ReadFromJsonAsync<WatchlistItemResponse>();
        Assert.NotNull(item);
        Assert.Equal($"/api/watchlist/{item.Id}", created.Headers.Location?.ToString());

        // Sesja powstaje leniwie, przy pierwszym zapisie
        await app.WithDatabaseAsync(async db => Assert.True(await db.Sessions.AnyAsync(s => s.Id == session)));

        var patched = await client.SendAsync(Patch(session, item.Id, new UpdateWatchlistItemRequest("issue wzięte, czekam na review")));
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);

        var listed = await client.SendAsync(Get(session));
        var items = await listed.Content.ReadFromJsonAsync<WatchlistItemResponse[]>();

        var only = Assert.Single(items!);
        Assert.Equal("issue wzięte, czekam na review", only.Note);
        Assert.Equal(Repo, only.RepoFullName);

        // Metadane dokładane z cache'u, bez ani jednego wywołania do GitHuba
        Assert.NotNull(only.Repo);
        Assert.Equal(8_900, only.Repo.Stars);
        Assert.Equal(84.0, only.Repo.HealthScore);

        var deleted = await client.SendAsync(Delete(session, item.Id));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var empty = await client.SendAsync(Get(session));
        Assert.Empty((await empty.Content.ReadFromJsonAsync<WatchlistItemResponse[]>())!);
    }

    [Fact]
    public async Task Pozycja_innej_sesji_jest_niewidoczna()
    {
        var client = app.CreateClientWithSchema();
        var owner = Guid.NewGuid();
        var intruder = Guid.NewGuid();

        var created = await client.SendAsync(Post(owner, new CreateWatchlistItemRequest("dotnet/aspnetcore", null)));
        var item = (await created.Content.ReadFromJsonAsync<WatchlistItemResponse>())!;

        var listed = await client.SendAsync(Get(intruder));
        Assert.Empty((await listed.Content.ReadFromJsonAsync<WatchlistItemResponse[]>())!);

        // Cudza pozycja ma wyglądać na nieistniejącą, nie na zabronioną
        var patched = await client.SendAsync(Patch(intruder, item.Id, new UpdateWatchlistItemRequest("cudza notatka")));
        Assert.Equal(HttpStatusCode.NotFound, patched.StatusCode);

        var deleted = await client.SendAsync(Delete(intruder, item.Id));
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task To_samo_repo_dwa_razy_w_sesji_konczy_sie_409()
    {
        var client = app.CreateClientWithSchema();
        var session = Guid.NewGuid();

        var first = await client.SendAsync(Post(session, new CreateWatchlistItemRequest("vuejs/core", null)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.SendAsync(Post(session, new CreateWatchlistItemRequest("vuejs/core", "druga próba")));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // Nazwa wklepana małymi literami ma zwrócić te same metadane co lista, która
    // zestawia je bez rozróżniania wielkości liter.
    [Fact]
    public async Task Zapis_nazwy_w_innym_zapisie_liter_zwraca_metadane_z_cachu()
    {
        var client = app.CreateClientWithSchema();
        var session = Guid.NewGuid();

        await app.WithDatabaseAsync(async db =>
        {
            if (!await db.RepoCache.AnyAsync(r => r.FullName == Repo)) db.RepoCache.Add(CachedRepo());
        });

        var created = await client.SendAsync(Post(session, new CreateWatchlistItemRequest(Repo.ToLowerInvariant(), null)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var item = (await created.Content.ReadFromJsonAsync<WatchlistItemResponse>())!;

        Assert.NotNull(item.Repo);
        Assert.Equal(8_900, item.Repo.Stars);
    }

    [Fact]
    public async Task Brak_naglowka_sesji_konczy_sie_400()
    {
        var client = app.CreateClientWithSchema();

        var response = await client.PostAsJsonAsync("/api/watchlist", new CreateWatchlistItemRequest(Repo, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    static HttpRequestMessage Get(Guid session) =>
        WithSession(new HttpRequestMessage(HttpMethod.Get, "/api/watchlist"), session);

    static HttpRequestMessage Post(Guid session, CreateWatchlistItemRequest body) =>
        WithSession(new HttpRequestMessage(HttpMethod.Post, "/api/watchlist")
        {
            Content = JsonContent.Create(body),
        }, session);

    static HttpRequestMessage Patch(Guid session, long id, UpdateWatchlistItemRequest body) =>
        WithSession(new HttpRequestMessage(HttpMethod.Patch, $"/api/watchlist/{id}")
        {
            Content = JsonContent.Create(body),
        }, session);

    static HttpRequestMessage Delete(Guid session, long id) =>
        WithSession(new HttpRequestMessage(HttpMethod.Delete, $"/api/watchlist/{id}"), session);

    static HttpRequestMessage WithSession(HttpRequestMessage request, Guid session)
    {
        request.Headers.Add("X-Session-Id", session.ToString());

        return request;
    }

    static RepoCacheEntry CachedRepo() => new()
    {
        FullName = Repo,
        Data = new RepoCandidate(
            Repo,
            "Blazor Component Library",
            "https://github.com/MudBlazor/MudBlazor",
            8_900,
            "C#",
            ["blazor", "material-design"],
            12_000,
            new DateTimeOffset(2026, 7, 27, 10, 12, 0, TimeSpan.Zero),
            "MIT",
            false,
            false,
            true,
            120),
        FetchedAt = DateTimeOffset.UtcNow,
        HealthScore = 84.0m,
        HealthBreakdown = [new ScoreComponent("merge_rate", "Odsetek zmergowanych PR-ów", 25, 25, "100 %", "wszystkie rozstrzygnięte PR-y zostały zmergowane")],
        HealthComputedAt = DateTimeOffset.UtcNow,
    };
}
