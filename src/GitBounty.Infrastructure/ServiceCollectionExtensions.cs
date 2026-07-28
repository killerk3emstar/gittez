using System.Net.Http.Headers;
using GitBounty.Core.Abstractions;
using GitBounty.Infrastructure.GitHub;
using GitBounty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;

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
