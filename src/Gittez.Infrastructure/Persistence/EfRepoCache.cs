using Gittez.Core.Abstractions;
using Gittez.Core.Models;
using Gittez.Core.Recommendations;
using Gittez.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gittez.Infrastructure.Persistence;

// Krótko żyjący kontekst na operację: pipeline woła cache z ośmiu zadań naraz,
// a DbContext nie jest bezpieczny wątkowo.
public sealed class EfRepoCache(
    IDbContextFactory<GittezDbContext> contextFactory,
    TimeProvider time) : IRepoCache
{
    public async Task<CachedProfile?> GetProfileAsync(string login, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var row = await db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.GithubLogin == login, ct);

        if (row is null) return null;

        var profile = new UserProfile(
            row.GithubLogin,
            [.. row.TopLanguages.Select(l => new UserLanguage(l.Name, l.OwnedRepos, l.ContributedRepos))],
            row.MedianSizeKb,
            row.Interests,
            row.PublicRepoCount);

        return new CachedProfile(profile, row.ComputedAt, IsFresh(row.ComputedAt, CacheTtl.Profile));
    }

    public async Task SaveProfileAsync(UserProfile profile, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var row = await db.Profiles.FirstOrDefaultAsync(p => p.GithubLogin == profile.Login, ct);
        if (row is null)
        {
            row = new ProfileEntity { GithubLogin = profile.Login };
            db.Profiles.Add(row);
        }

        row.TopLanguages = [.. profile.Languages.Select(l => new ProfileLanguage(l.Name, l.OwnedRepos, l.ContributedRepos, 0))];
        row.MedianSizeKb = profile.MedianSizeKb;
        row.Interests = [.. profile.Interests];
        row.PublicRepoCount = profile.PublicRepoCount;
        row.ComputedAt = time.GetUtcNow();

        await SaveIgnoringRacesAsync(db, ct);
    }

    public async Task<CachedIssues?> GetIssuesAsync(string fullName, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var rows = await db.IssueCache.AsNoTracking()
            .Where(i => i.RepoFullName == fullName)
            .ToListAsync(ct);

        if (rows.Count == 0) return null;

        var etag = await db.RepoCache.AsNoTracking()
            .Where(r => r.FullName == fullName)
            .Select(r => r.ETag)
            .FirstOrDefaultAsync(ct);

        var fetchedAt = rows.Max(r => r.FetchedAt);

        return new CachedIssues(
            [.. rows.Select(ToScoredIssue)],
            etag,
            fetchedAt,
            IsFresh(fetchedAt, CacheTtl.Issues));
    }

    public async Task SaveIssuesAsync(
        RepoCandidate repo, IReadOnlyList<ScoredIssue> issues, string? etag, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var now = time.GetUtcNow();

        await db.IssueCache.Where(i => i.RepoFullName == repo.FullName).ExecuteDeleteAsync(ct);

        db.IssueCache.AddRange(issues.Select(i => new IssueCacheEntry
        {
            Id = i.Issue.Id,
            RepoFullName = repo.FullName,
            Number = i.Issue.Number,
            Title = i.Issue.Title,
            HtmlUrl = i.Issue.HtmlUrl,
            Labels = [.. i.Issue.Labels],
            CommentCount = i.Issue.CommentCount,
            BodyLength = i.Issue.BodyLength,
            HasAssignee = i.Issue.HasAssignee,
            Difficulty = (short)i.Difficulty,
            IssueCreatedAt = i.Issue.CreatedAt,
            IssueUpdatedAt = i.Issue.UpdatedAt,
            FetchedAt = now,
        }));

        var row = await UpsertRepoAsync(db, repo, now, ct);
        row.ETag = etag;

        await SaveIgnoringRacesAsync(db, ct);
    }

    public async Task TouchIssuesAsync(string fullName, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var now = time.GetUtcNow();

        await db.IssueCache.Where(i => i.RepoFullName == fullName)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.FetchedAt, now), ct);

        await db.RepoCache.Where(r => r.FullName == fullName)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.FetchedAt, now), ct);
    }

    public async Task<CachedHealth?> GetHealthAsync(string fullName, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var row = await db.RepoCache.AsNoTracking()
            .FirstOrDefaultAsync(r => r.FullName == fullName, ct);

        if (row?.HealthComputedAt is not { } computedAt || row.HealthBreakdown is null) return null;

        return new CachedHealth(
            row.HealthScore is { } score ? (double)score : null,
            row.HealthBreakdown,
            computedAt,
            IsFresh(computedAt, CacheTtl.Health));
    }

    public async Task SaveHealthAsync(
        RepoCandidate repo, double? score, IReadOnlyList<ScoreComponent> breakdown, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var now = time.GetUtcNow();

        var row = await UpsertRepoAsync(db, repo, now, ct);
        row.HealthScore = score is { } s ? Math.Round((decimal)s, 2) : null;
        row.HealthBreakdown = [.. breakdown];
        row.HealthComputedAt = now;

        await SaveIgnoringRacesAsync(db, ct);
    }

    public async Task<IReadOnlyList<CachedCandidate>> GetCandidatesAsync(
        IReadOnlyList<string> languages, int starsLo, int starsHi, int limit, CancellationToken ct = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        // Tabela cache'u jest z założenia mała (seed to ~40 pozycji), więc
        // filtrowanie po polach jsonb robimy po stronie aplikacji.
        var rows = await db.RepoCache.AsNoTracking().ToListAsync(ct);

        var wanted = new HashSet<string>(languages, StringComparer.OrdinalIgnoreCase);

        var matching = rows
            .Where(r => r.Data.PrimaryLanguage is not null && wanted.Contains(r.Data.PrimaryLanguage))
            .Where(r => r.Data.Stars >= starsLo && r.Data.Stars <= starsHi)
            .OrderByDescending(r => r.HealthScore ?? 0)
            .Take(limit)
            .ToList();

        if (matching.Count == 0) return [];

        var names = matching.Select(r => r.FullName).ToArray();

        var issues = await db.IssueCache.AsNoTracking()
            .Where(i => names.Contains(i.RepoFullName))
            .ToListAsync(ct);

        var issuesByRepo = issues
            .GroupBy(i => i.RepoFullName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScoredIssue>)[.. g.Select(ToScoredIssue)],
                StringComparer.OrdinalIgnoreCase);

        return
        [
            .. matching.Select(r => new CachedCandidate(
                r.Data,
                r.HealthBreakdown is null || r.HealthComputedAt is null
                    ? null
                    : new CachedHealth(
                        r.HealthScore is { } score ? (double)score : null,
                        r.HealthBreakdown,
                        r.HealthComputedAt.Value,
                        IsFresh(r.HealthComputedAt.Value, CacheTtl.Health)),
                issuesByRepo.GetValueOrDefault(r.FullName, []),
                r.FetchedAt))
        ];
    }

    static async Task<RepoCacheEntry> UpsertRepoAsync(
        GittezDbContext db, RepoCandidate repo, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.RepoCache.FirstOrDefaultAsync(r => r.FullName == repo.FullName, ct);
        if (row is null)
        {
            row = new RepoCacheEntry { FullName = repo.FullName, Data = repo, FetchedAt = now };
            db.RepoCache.Add(row);
        }
        else
        {
            row.Data = repo;
            row.FetchedAt = now;
        }

        return row;
    }

    // Dwa równoległe przebiegi mogą wstawiać ten sam klucz; przegrany po prostu
    // nie nadpisuje danych, które i tak są identyczne.
    static async Task SaveIgnoringRacesAsync(GittezDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
        }
    }

    static ScoredIssue ToScoredIssue(IssueCacheEntry i) => new(
        new IssueSummary(
            i.Id, i.Number, i.Title, i.HtmlUrl, i.Labels, i.CommentCount, i.BodyLength,
            i.HasAssignee, i.IssueCreatedAt, i.IssueUpdatedAt),
        i.Difficulty);

    bool IsFresh(DateTimeOffset at, TimeSpan ttl) => time.GetUtcNow() - at < ttl;
}
