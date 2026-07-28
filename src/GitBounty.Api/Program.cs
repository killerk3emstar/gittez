using GitBounty.Api;
using GitBounty.Api.Endpoints;
using GitBounty.Core.Recommendations;
using GitBounty.Infrastructure;
using GitBounty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Brak connection stringa - ustaw ConnectionStrings__Default");

builder.Services.AddGitBountyPersistence(connectionString);
builder.Services.AddGitHubClient(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RecommendationPipeline>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GitHubExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Configuration.GetValue("APPLY_MIGRATIONS", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<GitBountyDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthEndpoints();
app.MapProfileEndpoints();
app.MapRecommendationEndpoints();

app.Run();
