using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GitBounty.Infrastructure.Persistence;

// Seed leci PO migracjach, nie przez /docker-entrypoint-initdb.d/: skrypty
// initdb odpalają się zanim EF założy tabele i przerwałyby start kontenera
// (SPEC §8.1). Skrypt jest idempotentny przez ON CONFLICT DO NOTHING.
public sealed class DatabaseSeeder(GitBountyDbContext db, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            logger.LogWarning("Pominięto seed, brak pliku {Path}", path);
            return;
        }

        var sql = await File.ReadAllTextAsync(path, ct);
        if (string.IsNullOrWhiteSpace(sql)) return;

        try
        {
            // Surowe ADO zamiast ExecuteSqlRaw: ta druga przepuszcza SQL przez
            // string.Format, a seed jest pełen nawiasów klamrowych z jsonb.
            await db.Database.OpenConnectionAsync(ct);

            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            var rows = await command.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Seed z {Path} wstawił {Rows} wierszy", path, rows);
        }
        catch (Exception ex)
        {
            // Brak seeda nie może przewrócić startu - aplikacja działa wtedy
            // wyłącznie na świeżych danych z GitHuba.
            logger.LogError(ex, "Seed z {Path} nie wszedł", path);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
