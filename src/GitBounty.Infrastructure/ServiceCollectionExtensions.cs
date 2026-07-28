using GitBounty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GitBounty.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitBountyPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        // bez tego Npgsql odmawia serializacji kolumn jsonb do typów POCO
        dataSourceBuilder.EnableDynamicJson();

        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);
        services.AddDbContext<GitBountyDbContext>(o => o.UseNpgsql(dataSource));

        return services;
    }
}
