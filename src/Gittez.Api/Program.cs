using Gittez.Api;
using Gittez.Api.Endpoints;
using Gittez.Core.Profiles;
using Gittez.Core.Recommendations;
using Gittez.Infrastructure;
using Gittez.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Brak connection stringa - ustaw ConnectionStrings__Default");

builder.Services.AddGittezPersistence(connectionString);
builder.Services.AddGitHubClient(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ProfileProvider>();
builder.Services.AddScoped<RecommendationPipeline>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GitHubExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue("APPLY_MIGRATIONS", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<GittezDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue("SEED_ON_STARTUP", false))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(Path.Combine(app.Environment.ContentRootPath, "db", "seed", "repo_cache_seed.sql"));
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthEndpoints();
app.MapProfileEndpoints();
app.MapRecommendationEndpoints();
app.MapWatchlistEndpoints();

app.Run();

// widoczne dla WebApplicationFactory w testach integracyjnych
public partial class Program;
