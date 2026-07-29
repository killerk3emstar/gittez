using GitBounty.Api.Contracts;
using GitBounty.Infrastructure.Persistence;
using GitBounty.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GitBounty.Api.Endpoints;

public static class WatchlistEndpoints
{
    const int MaxNoteLength = 500;

    // Demo jest publiczne i bez logowania, więc pojedyncza sesja mogłaby wstawić
    // dowolnie wiele wierszy. Sto pozycji to znacznie więcej, niż da się przejrzeć.
    const int MaxItemsPerSession = 100;

    public static IEndpointRouteBuilder MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/watchlist").WithTags("Watchlist");

        group.MapGet("", async (
            HttpContext http,
            GitBountyDbContext db,
            CancellationToken ct) =>
        {
            if (!Session.TryRead(http, out var sessionId)) return Session.Missing();

            var items = await db.WatchlistItems.AsNoTracking()
                .Where(i => i.SessionId == sessionId)
                .OrderByDescending(i => i.Id)
                .ToListAsync(ct);

            if (items.Count == 0) return Results.Ok(Array.Empty<WatchlistItemResponse>());

            var names = items.Select(i => i.RepoFullName).ToArray();

            var repos = await db.RepoCache.AsNoTracking()
                .Where(r => names.Contains(r.FullName))
                .ToListAsync(ct);

            var byName = repos.ToDictionary(r => r.FullName, StringComparer.OrdinalIgnoreCase);

            return Results.Ok(items.Select(i => ToResponse(i, byName.GetValueOrDefault(i.RepoFullName))));
        })
        .WithName("GetWatchlist")
        .WithSummary("Pozycje watchlisty bieżącej sesji");

        group.MapPost("", async (
            CreateWatchlistItemRequest request,
            HttpContext http,
            GitBountyDbContext db,
            TimeProvider time,
            CancellationToken ct) =>
        {
            if (!Session.TryRead(http, out var sessionId)) return Session.Missing();

            var fullName = request.RepoFullName?.Trim() ?? string.Empty;
            if (!IsRepoFullName(fullName))
            {
                return Results.Problem(
                    type: "invalid-repo-name",
                    title: "Nieprawidłowa nazwa repozytorium",
                    detail: "Oczekiwany format to owner/name.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!TryNormalizeNote(request.Note, out var note)) return NoteTooLong();

            var duplicate = await db.WatchlistItems
                .AnyAsync(i => i.SessionId == sessionId && i.RepoFullName.ToLower() == fullName.ToLower(), ct);

            if (duplicate) return AlreadyOnWatchlist(fullName);

            var count = await db.WatchlistItems.CountAsync(i => i.SessionId == sessionId, ct);
            if (count >= MaxItemsPerSession) return WatchlistFull();

            var now = time.GetUtcNow();
            await TouchSessionAsync(db, sessionId, now, ct);

            var item = new WatchlistItem
            {
                SessionId = sessionId,
                RepoFullName = fullName,
                Note = note,
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.WatchlistItems.Add(item);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Podwójne kliknięcie w gwiazdkę: sprawdzenie wyżej przegrywa
                // wyścig, rozstrzyga unikalny indeks (session_id, repo_full_name).
                return AlreadyOnWatchlist(fullName);
            }

            var repo = await db.RepoCache.AsNoTracking()
                .FirstOrDefaultAsync(r => r.FullName == fullName, ct);

            return Results.Created($"/api/watchlist/{item.Id}", ToResponse(item, repo));
        })
        .WithName("AddToWatchlist")
        .WithSummary("Zapisanie repozytorium na watchliście sesji");

        group.MapPatch("/{id:long}", async (
            long id,
            UpdateWatchlistItemRequest request,
            HttpContext http,
            GitBountyDbContext db,
            TimeProvider time,
            CancellationToken ct) =>
        {
            if (!Session.TryRead(http, out var sessionId)) return Session.Missing();
            if (!TryNormalizeNote(request.Note, out var note)) return NoteTooLong();

            // Filtr po sesji, nie tylko po id: cudza pozycja ma wyglądać na
            // nieistniejącą, a nie na zabronioną.
            var item = await db.WatchlistItems
                .FirstOrDefaultAsync(i => i.Id == id && i.SessionId == sessionId, ct);

            if (item is null) return NotFound(id);

            var now = time.GetUtcNow();
            item.Note = note;
            item.UpdatedAt = now;
            await TouchSessionAsync(db, sessionId, now, ct);
            await db.SaveChangesAsync(ct);

            var repo = await db.RepoCache.AsNoTracking()
                .FirstOrDefaultAsync(r => r.FullName == item.RepoFullName, ct);

            return Results.Ok(ToResponse(item, repo));
        })
        .WithName("UpdateWatchlistNote")
        .WithSummary("Edycja notatki przy zapisanym repozytorium");

        group.MapDelete("/{id:long}", async (
            long id,
            HttpContext http,
            GitBountyDbContext db,
            CancellationToken ct) =>
        {
            if (!Session.TryRead(http, out var sessionId)) return Session.Missing();

            var deleted = await db.WatchlistItems
                .Where(i => i.Id == id && i.SessionId == sessionId)
                .ExecuteDeleteAsync(ct);

            return deleted == 0 ? NotFound(id) : Results.NoContent();
        })
        .WithName("RemoveFromWatchlist")
        .WithSummary("Usunięcie pozycji z watchlisty");

        return app;
    }

    // Sesja powstaje przy pierwszym zapisie: czytelnicy nie zakładają wierszy,
    // więc tabela nie puchnie od odwiedzin, które niczego nie zapisały.
    static async Task TouchSessionAsync(
        GitBountyDbContext db, Guid sessionId, DateTimeOffset now, CancellationToken ct)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
        {
            db.Sessions.Add(new SessionEntity { Id = sessionId, CreatedAt = now, LastSeenAt = now });
            return;
        }

        session.LastSeenAt = now;
    }

    static bool TryNormalizeNote(string? raw, out string? note)
    {
        note = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

        return note is null || note.Length <= MaxNoteLength;
    }

    // Zestaw znaków dopuszczany przez GitHuba, nie samo "cokolwiek ze slashem":
    // nazwa trafia potem do linków w UI.
    static bool IsRepoFullName(string value)
    {
        var parts = value.Split('/');

        return parts.Length == 2
            && parts.All(p => p.Length is > 0 and <= 100 && p.All(IsNameChar));

        static bool IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';
    }

    static IResult AlreadyOnWatchlist(string fullName) => Results.Problem(
        type: "already-on-watchlist",
        title: "Repozytorium jest już na watchliście",
        detail: $"{fullName} zostało już zapisane w tej sesji.",
        statusCode: StatusCodes.Status409Conflict);

    static IResult WatchlistFull() => Results.Problem(
        type: "watchlist-full",
        title: "Watchlista jest pełna",
        detail: $"Jedna sesja mieści {MaxItemsPerSession} pozycji. Usuń coś, żeby zrobić miejsce.",
        statusCode: StatusCodes.Status409Conflict);

    static IResult NoteTooLong() => Results.Problem(
        type: "note-too-long",
        title: "Notatka jest za długa",
        detail: $"Maksymalna długość notatki to {MaxNoteLength} znaków.",
        statusCode: StatusCodes.Status400BadRequest);

    static IResult NotFound(long id) => Results.Problem(
        type: "watchlist-item-not-found",
        title: "Nie ma takiej pozycji",
        detail: $"Pozycja {id} nie istnieje w tej sesji.",
        statusCode: StatusCodes.Status404NotFound);

    static WatchlistItemResponse ToResponse(WatchlistItem item, RepoCacheEntry? cached) => new(
        item.Id,
        item.RepoFullName,
        item.Note,
        item.CreatedAt,
        item.UpdatedAt,
        cached is null ? null : new WatchlistRepoResponse(
            cached.Data.Description,
            cached.Data.HtmlUrl,
            cached.Data.Stars,
            cached.Data.PrimaryLanguage,
            cached.Data.PushedAt,
            cached.HealthScore is { } health ? (double)health : null));
}
