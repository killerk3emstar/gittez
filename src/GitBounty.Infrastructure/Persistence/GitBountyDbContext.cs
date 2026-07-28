using System.Text.Json;
using GitBounty.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GitBounty.Infrastructure.Persistence;

public class GitBountyDbContext(DbContextOptions<GitBountyDbContext> options) : DbContext(options)
{
    public DbSet<ProfileEntity> Profiles => Set<ProfileEntity>();
    public DbSet<RepoCacheEntry> RepoCache => Set<RepoCacheEntry>();
    public DbSet<IssueCacheEntry> IssueCache => Set<IssueCacheEntry>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ProfileEntity>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(x => x.GithubLogin);
            e.Property(x => x.GithubLogin).HasColumnName("github_login").HasMaxLength(64);
            AsJson(e.Property(x => x.TopLanguages).HasColumnName("top_languages"));
            e.Property(x => x.MedianSizeKb).HasColumnName("median_size_kb");
            e.Property(x => x.Interests).HasColumnName("interests").HasColumnType("jsonb");
            e.Property(x => x.PublicRepoCount).HasColumnName("public_repo_count");
            e.Property(x => x.ComputedAt).HasColumnName("computed_at");
        });

        b.Entity<RepoCacheEntry>(e =>
        {
            e.ToTable("repo_cache");
            e.HasKey(x => x.FullName);
            e.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255);
            AsJson(e.Property(x => x.Data).HasColumnName("data"));
            e.Property(x => x.ETag).HasColumnName("etag").HasMaxLength(128);
            e.Property(x => x.FetchedAt).HasColumnName("fetched_at");
            e.Property(x => x.HealthScore).HasColumnName("health_score").HasPrecision(5, 2);
            AsJson(e.Property(x => x.HealthBreakdown).HasColumnName("health_breakdown")!);
            e.Property(x => x.HealthComputedAt).HasColumnName("health_computed_at");
            e.HasIndex(x => x.FetchedAt).HasDatabaseName("ix_repo_cache_fetched");
        });

        b.Entity<IssueCacheEntry>(e =>
        {
            e.ToTable("issue_cache");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.RepoFullName).HasColumnName("repo_full_name").HasMaxLength(255);
            e.Property(x => x.Number).HasColumnName("number");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.HtmlUrl).HasColumnName("html_url");
            e.Property(x => x.Labels).HasColumnName("labels").HasColumnType("jsonb");
            e.Property(x => x.CommentCount).HasColumnName("comment_count");
            e.Property(x => x.BodyLength).HasColumnName("body_length");
            e.Property(x => x.HasAssignee).HasColumnName("has_assignee");
            e.Property(x => x.Difficulty).HasColumnName("difficulty");
            e.Property(x => x.IssueCreatedAt).HasColumnName("issue_created_at");
            e.Property(x => x.IssueUpdatedAt).HasColumnName("issue_updated_at");
            e.Property(x => x.FetchedAt).HasColumnName("fetched_at");
            e.HasIndex(x => x.RepoFullName).HasDatabaseName("ix_issue_cache_repo");
        });

        b.Entity<SessionEntity>(e =>
        {
            e.ToTable("sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        });

        b.Entity<WatchlistItem>(e =>
        {
            e.ToTable("watchlist_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.RepoFullName).HasColumnName("repo_full_name").HasMaxLength(255);
            e.Property(x => x.Note).HasColumnName("note");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Session)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SessionId, x.RepoFullName }).IsUnique();
            e.HasIndex(x => x.SessionId).HasDatabaseName("ix_watchlist_session");
        });
    }

    // Kolumna zostaje jsonb, ale serializację robimy sami zamiast zdawać się na
    // mapowanie POCO w Npgsql - dzięki temu model buduje się na dowolnym
    // providerze i test integracyjny działa na SQLite, bez Testcontainers.
    // Kształt JSON-a jest ten sam co wcześniej, więc seed pozostaje zgodny.
    static void AsJson<T>(PropertyBuilder<T> property) where T : class? =>
        property
            .HasColumnType("jsonb")
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                json => JsonSerializer.Deserialize<T>(json, JsonOptions)!,
                new ValueComparer<T>(
                    (a, b) => Json(a) == Json(b),
                    value => Json(value).GetHashCode(),
                    value => JsonSerializer.Deserialize<T>(Json(value), JsonOptions)!));

    static string Json<T>(T? value) => JsonSerializer.Serialize(value, JsonOptions);

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);
}
