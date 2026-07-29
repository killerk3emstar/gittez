using Gittez.Core.Abstractions;
using Gittez.Core.Models;
using Gittez.Core.Recommendations;
using Gittez.Tests.Scoring;

namespace Gittez.Tests.Recommendations;

sealed class FakeTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

sealed class FakeGitHub : IGitHubClient
{
    public List<RepoCandidate> Found { get; } = [];

    public Exception? SearchFailure { get; set; }

    // Zwrot per repozytorium: test opisuje, które z nich odpowiada, a które nie.
    public Func<string, IReadOnlyList<IssueSummary>> Issues { get; set; } = _ => [];

    public Task<IReadOnlyList<OwnedRepo>> GetOwnedReposAsync(string login, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OwnedRepo>>([]);

    public Task<IReadOnlyList<RepoCandidate>> SearchRepositoriesAsync(
        string language, int starsLo, int starsHi, CancellationToken ct = default) =>
        SearchFailure is not null
            ? Task.FromException<IReadOnlyList<RepoCandidate>>(SearchFailure)
            : Task.FromResult<IReadOnlyList<RepoCandidate>>(Found);

    public Task<GitHubResult<IReadOnlyList<IssueSummary>>> GetGoodFirstIssuesAsync(
        string fullName, string? etag = null, CancellationToken ct = default) =>
        Task.FromResult(new GitHubResult<IReadOnlyList<IssueSummary>>(Issues(fullName), null, false));

    public Task<HealthInput> GetHealthInputAsync(
        string fullName, DateTimeOffset pushedAt, CancellationToken ct = default) =>
        Task.FromResult(new HealthInput([], [], [], pushedAt));
}

sealed class FakeCache : IRepoCache
{
    public UserProfile? Profile { get; set; }

    public List<CachedCandidate> Candidates { get; } = [];

    public Task<CachedProfile?> GetProfileAsync(string login, CancellationToken ct = default) =>
        Task.FromResult(Profile is null ? null : new CachedProfile(Profile, Build.Now, true));

    public Task SaveProfileAsync(UserProfile profile, CancellationToken ct = default) => Task.CompletedTask;

    public Task<CachedIssues?> GetIssuesAsync(string fullName, CancellationToken ct = default) =>
        Task.FromResult<CachedIssues?>(null);

    public Task SaveIssuesAsync(
        RepoCandidate repo, IReadOnlyList<ScoredIssue> issues, string? etag, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task TouchIssuesAsync(string fullName, CancellationToken ct = default) => Task.CompletedTask;

    public Task<CachedHealth?> GetHealthAsync(string fullName, CancellationToken ct = default) =>
        Task.FromResult<CachedHealth?>(null);

    public Task SaveHealthAsync(
        RepoCandidate repo, double? score, IReadOnlyList<ScoreComponent> breakdown, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<CachedCandidate>> GetCandidatesAsync(
        IReadOnlyList<string> languages, int starsLo, int starsHi, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CachedCandidate>>(Candidates);
}
