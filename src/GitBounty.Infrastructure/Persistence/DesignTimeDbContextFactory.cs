using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GitBounty.Infrastructure.Persistence;

// Migracje nie potrzebują konfiguracji Api ani działającej bazy - dzięki temu
// dotnet ef działa na czystym klonie.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GitBountyDbContext>
{
    public GitBountyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=gitbounty;Username=gitbounty;Password=gitbounty";

        var options = new DbContextOptionsBuilder<GitBountyDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new GitBountyDbContext(options);
    }
}
