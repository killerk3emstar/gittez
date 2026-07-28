using GitBounty.Core.Abstractions;
using GitBounty.Core.Models;
using GitBounty.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace GitBounty.Tests.Integration;

// SQLite w pamięci zamiast Testcontainers: ten test pilnuje ścieżki zapisu
// (SPEC §10), a nie dialektu Postgresa - narzut kontenera byłby większy niż zysk.
public sealed class GitBountyApp : WebApplicationFactory<Program>
{
    // Połączenie musi żyć tak długo jak baza: SQLite kasuje ją przy zamknięciu.
    readonly SqliteConnection connection = new("Filename=:memory:");

    public GitBountyApp() => connection.Open();

    public HttpClient CreateClientWithSchema()
    {
        var client = CreateClient();

        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<GitBountyDbContext>().Database.EnsureCreated();

        return client;
    }

    public async Task WithDatabaseAsync(Func<GitBountyDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GitBountyDbContext>();

        await action(db);
        await db.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", "Host=nieuzywany;Database=gitbounty;Username=x;Password=x");
        builder.UseSetting("APPLY_MIGRATIONS", "false");
        builder.UseSetting("SEED_ON_STARTUP", "false");
    }

    // Podmiana idzie tędy, nie przez ConfigureWebHost: przy hostingu minimalnym
    // tamte wywołania zwrotne wykonują się PRZED rejestracjami z Program.cs, więc
    // Npgsql dokładałby się do opcji już po nas i EF widziałby dwa providery.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            foreach (var registration in services
                .Where(d => d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericArguments().Contains(typeof(GitBountyDbContext)))
                .ToList())
            {
                services.Remove(registration);
            }

            services.RemoveAll<NpgsqlDataSource>();
            services.AddDbContextFactory<GitBountyDbContext>(o => o.UseSqlite(connection));

            // Watchlista nie rusza GitHuba; podmiana jest po to, żeby test nie
            // miał jak wyjść do sieci nawet przez pomyłkę.
            services.RemoveAll<IGitHubClient>();
            services.AddSingleton<IGitHubClient, UnreachableGitHub>();
        });

        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing) connection.Dispose();
    }
}

sealed class UnreachableGitHub : IGitHubClient
{
    public Task<IReadOnlyList<OwnedRepo>> GetOwnedReposAsync(string login, CancellationToken ct = default) =>
        throw new NotSupportedException("Test nie powinien wołać GitHuba");

    public Task<IReadOnlyList<RepoCandidate>> SearchRepositoriesAsync(
        string language, int starsLo, int starsHi, CancellationToken ct = default) =>
        throw new NotSupportedException("Test nie powinien wołać GitHuba");

    public Task<GitHubResult<IReadOnlyList<IssueSummary>>> GetGoodFirstIssuesAsync(
        string fullName, string? etag = null, CancellationToken ct = default) =>
        throw new NotSupportedException("Test nie powinien wołać GitHuba");

    public Task<HealthInput> GetHealthInputAsync(
        string fullName, DateTimeOffset pushedAt, CancellationToken ct = default) =>
        throw new NotSupportedException("Test nie powinien wołać GitHuba");
}
