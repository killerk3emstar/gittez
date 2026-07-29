using Gittez.Core.Models;

namespace Gittez.Tests.Scoring;

static class Build
{
    public static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    public static RepoCandidate Repo(
        string? language = "C#",
        int stars = 500,
        int sizeKb = 1_000,
        IReadOnlyList<string>? topics = null,
        DateTimeOffset? pushedAt = null) =>
        new(
            FullName: "owner/name",
            Description: "opis",
            HtmlUrl: "https://github.com/owner/name",
            Stars: stars,
            PrimaryLanguage: language,
            Topics: topics ?? [],
            SizeKb: sizeKb,
            PushedAt: pushedAt ?? Now.AddDays(-1),
            License: "MIT",
            Archived: false,
            Disabled: false,
            HasIssues: true,
            OpenIssuesCount: 3);

    public static UserProfile User(
        IReadOnlyList<UserLanguage>? languages = null,
        IReadOnlyList<string>? interests = null) =>
        new(
            Login: "killerk3emstar",
            Languages: languages ?? [new UserLanguage("C#", 7, 2)],
            MedianSizeKb: 337,
            Interests: interests ?? [],
            PublicRepoCount: 8);

    public static PullSummary Pull(
        double createdDaysAgo = 1,
        double? closedDaysAgo = null,
        bool merged = false,
        bool draft = false,
        string login = "human",
        string type = "User")
    {
        var closed = closedDaysAgo is { } d ? Now.AddDays(-d) : (DateTimeOffset?)null;
        return new PullSummary(
            CreatedAt: Now.AddDays(-createdDaysAgo),
            ClosedAt: closed,
            MergedAt: merged ? closed : null,
            IsDraft: draft,
            AuthorLogin: login,
            AuthorType: type);
    }

    public static PullSummary ResolvedAfterHours(double hours, bool merged = true) =>
        new(
            CreatedAt: Now.AddHours(-hours - 1),
            ClosedAt: Now.AddHours(-1),
            MergedAt: merged ? Now.AddHours(-1) : null,
            IsDraft: false,
            AuthorLogin: "human",
            AuthorType: "User");

    public static ClosedIssue Issue(double closedAfterDays, bool isPullRequest = false) =>
        new(
            CreatedAt: Now.AddDays(-closedAfterDays - 1),
            ClosedAt: Now.AddDays(-1),
            IsPullRequest: isPullRequest);

    public static HealthInput Health(
        IReadOnlyList<PullSummary>? recentPulls = null,
        IReadOnlyList<PullSummary>? openPulls = null,
        IReadOnlyList<ClosedIssue>? closedIssues = null,
        double pushedDaysAgo = 1) =>
        new(
            RecentPulls: recentPulls ?? [],
            OldestOpenPulls: openPulls ?? [],
            ClosedIssues: closedIssues ?? [],
            PushedAt: Now.AddDays(-pushedDaysAgo));
}
