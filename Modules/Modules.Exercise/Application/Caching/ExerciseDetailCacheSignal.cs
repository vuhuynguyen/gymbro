using Microsoft.Extensions.Primitives;

namespace Modules.ExerciseModule.Application.Caching;

/// <summary>
/// A single change-token shared by every cached exercise-detail entry. Detail entries are keyed per
/// scope (one per tenant that has viewed the exercise, plus an "admin" scope), so the live keys for a
/// given exercise can't be enumerated for targeted eviction — a tenant only materializes its key the
/// first time it reads the exercise. Each detail entry therefore links to this signal instead. Any
/// catalog mutation (update/delete) calls <see cref="Invalidate"/>, which trips the token and evicts
/// <em>all</em> detail entries — across every tenant and the admin scope — at once. Mirrors
/// <see cref="ExerciseSearchCacheSignal"/>. Registered as a singleton so the one token instance is
/// shared by readers and writers.
/// </summary>
public sealed class ExerciseDetailCacheSignal
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

    /// <summary>Evict every detail entry currently linked to this signal, then start a fresh generation.</summary>
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
