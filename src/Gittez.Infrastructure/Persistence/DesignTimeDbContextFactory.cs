using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gittez.Infrastructure.Persistence;

// Migracje nie potrzebują konfiguracji Api ani działającej bazy - dzięki temu
// dotnet ef działa na czystym klonie.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GittezDbContext>
{
    public GittezDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=gittez;Username=gittez;Password=gittez";

        var options = new DbContextOptionsBuilder<GittezDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new GittezDbContext(options);
    }
}
