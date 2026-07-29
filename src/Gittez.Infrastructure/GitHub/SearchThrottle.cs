namespace Gittez.Infrastructure.GitHub;

// Pula search to 30/minutę i wchodzi się w nią łatwiej, niż się wydaje:
// przemiatanie 9 języków po 4 pasma zaczęło odrzucać wyniki po trzydziestce
// (SPEC §4.4 pkt 13). Wyszukiwania idą sekwencyjnie, z odstępem.
public sealed class SearchThrottle : IDisposable
{
    static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    readonly SemaphoreSlim _gate = new(1, 1);
    DateTimeOffset _lastCall = DateTimeOffset.MinValue;

    public async Task<T> RunAsync<T>(Func<Task<T>> search, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var wait = MinInterval - (DateTimeOffset.UtcNow - _lastCall);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);

            return await search();
        }
        finally
        {
            _lastCall = DateTimeOffset.UtcNow;
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
