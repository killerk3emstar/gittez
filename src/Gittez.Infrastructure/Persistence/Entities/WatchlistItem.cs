namespace Gittez.Infrastructure.Persistence.Entities;

public class WatchlistItem
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public SessionEntity? Session { get; set; }
    public required string RepoFullName { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
