namespace Gittez.Infrastructure.Persistence.Entities;

public class SessionEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public List<WatchlistItem> Items { get; set; } = [];
}
