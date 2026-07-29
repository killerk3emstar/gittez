using System.Net.Http.Headers;
using Gittez.Core.Abstractions;
using Gittez.Infrastructure.GitHub;
using Gittez.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;

namespace Gittez.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGittezPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

        // bez tego Npgsql odmawia serializacji kolumn jsonb do typów POCO
        dataSourceBuilder.EnableDynamicJson();

        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        // Fabryka, bo pipeline czyta cache z ośmiu zadań naraz; zakresowy
        // kontekst zostaje dla endpointów, które i tak są jednowątkowe.
        services.AddDbContextFactory<GittezDbContext>(o => o.UseNpgsql(dataSource));
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<GittezDbContext>>().CreateDbContext());

        services.AddScoped<IRepoCache, EfRepoCache>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    public static IServiceCollection AddGitHubClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(configuration.GetSection(GitHubOptions.Section));

        services.AddSingleton<RateLimitTracker>();
        services.AddSingleton<IRateLimitTracker>(sp => sp.GetRequiredService<RateLimitTracker>());
        services.AddSingleton<SearchThrottle>();

        services.AddHttpClient<IGitHubClient, GitHubClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;

            http.BaseAddress = new Uri(options.BaseAddress);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);

            if (!string.IsNullOrWhiteSpace(options.Token))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            }
        })
        .AddResilienceHandler("github", builder =>
        {
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = args => ValueTask.FromResult(GitHubResilience.ShouldRetry(args.Outcome)),
                DelayGenerator = args => ValueTask.FromResult(GitHubResilience.RetryAfter(args.Outcome)),
            });

            builder.AddTimeout(TimeSpan.FromSeconds(20));
        });

        return services;
    }
}
