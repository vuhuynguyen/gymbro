using Microsoft.Extensions.Primitives;

namespace Modules.ExerciseModule.Application.Caching;

/// <summary>
/// A single change-token shared by every cached search result. Search cache keys span a large
/// scope × filter × page permutation space that can't be enumerated for targeted eviction, so each
/// search entry instead links to this signal. Any catalog mutation (create/update/delete) calls
/// <see cref="Invalidate"/>, which trips the token and evicts <em>all</em> search entries — across
/// every tenant and the admin scope — at once. Registered as a singleton so the one token instance
/// is shared by readers and writers.
/// </summary>
public sealed class ExerciseSearchCacheSignal
{
    private readonly object _gate = new();
    private CancellationTokenSource _cts = new();

    /// <summary>A change token tied to the current generation. Add it to a cache entry's expiration
    /// tokens so the entry is evicted the next time <see cref="Invalidate"/> fires.</summary>
    public IChangeToken Token
    {
        get
        {
            lock (_gate)
                return new CancellationChangeToken(_cts.Token);
        }
    }

    /// <summary>Evict every search entry currently linked to this signal, then start a fresh generation.</summary>
    public void Invalidate()
    {
        CancellationTokenSource toCancel;
        lock (_gate)
        {
            toCancel = _cts;
            _cts = new CancellationTokenSource();
        }

        // Cancelling fires the eviction callbacks synchronously; disposing afterwards is safe.
        toCancel.Cancel();
        toCancel.Dispose();
    }
}
