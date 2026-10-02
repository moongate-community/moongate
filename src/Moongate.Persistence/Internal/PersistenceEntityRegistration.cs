using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Snapshots;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceEntityRegistration<T> : IPersistenceEntityRegistration where T : class, IMoongateEntity
{
    /// <summary>
    ///     Every this many saves, every captured entity is written whatever its fingerprint: a row changed behind the
    ///     world save, such as by a character leaving during a save, is put right within an hour at the default
    ///     five-minute interval.
    /// </summary>
    public const int FullWriteEvery = 12;

    private const int SnapshotBatchSize = 256;
    private readonly Func<IEnumerable<T>> _source;
    private readonly Func<T, T> _snapshot;
    private readonly IPersistenceDeletionSource? _deletions;
    private IReadOnlyCollection<Serial> _captured = [];

    // The fingerprints of the last committed save, and those of the save in progress, adopted on its commit: a failed
    // save leaves the old ones, so its entities are written again.
    private Dictionary<Serial, UInt128> _saved = [];
    private Dictionary<Serial, UInt128>? _pending;
    private int _saves;

    public Type EntityType => typeof(T);

    public int Written { get; private set; }

    public PersistenceEntityRegistration(
        Func<IEnumerable<T>> source,
        Func<T, T> snapshot,
        IPersistenceDeletionSource? deletions = null
    )
    {
        _source = source;
        _snapshot = snapshot;
        _deletions = deletions;
    }

    public Func<PersistenceTransaction, CancellationToken, Task> Capture(out int entityCount)
    {
        var values = new List<T>();
        var ids = new HashSet<Serial>();
        var source = _source() ?? throw new InvalidOperationException("A persistence source returned null.");

        foreach (var live in source)
        {
            if (live is null || !live.Id.IsValid)
            {
                throw new InvalidOperationException("A persistence source has a null entity or zero identity.");
            }

            var identity = live.Id;
            var value = _snapshot(live);

            if (value is null ||
                ReferenceEquals(live, value) ||
                value.Id != identity ||
                live.Id != identity ||
                !value.Id.IsValid ||
                !ids.Add(value.Id))
            {
                throw new InvalidOperationException(
                    $"Snapshot for '{typeof(T).FullName}' must be detached with an unchanged, unique, nonzero identity."
                );
            }

            values.Add(value);
        }

        entityCount = values.Count;
        var deletions = (_deletions?.Capture() ?? []).Where(id => !ids.Contains(id)).ToArray();
        _captured = deletions;
        _pending = null;
        Written = 0;
        // The first save and every FullWriteEvery-th one after it write everything.
        var full = _saves++ % FullWriteEvery == 0;

        return async (transaction, cancellationToken) =>
        {
            // Off the loop: only the snapshots that changed since the last committed save are written. An entity no
            // longer captured drops out of the fingerprints, so it is written again if it comes back.
            var saved = _saved;
            var pending = new Dictionary<Serial, UInt128>(values.Count);
            var changed = new List<T>();

            foreach (var value in values)
            {
                var fingerprint = SnapshotFingerprint.Of(value);
                pending[value.Id] = fingerprint;

                if (full || !saved.TryGetValue(value.Id, out var previous) || previous != fingerprint)
                {
                    changed.Add(value);
                }
            }

            // Deletions first: a deleted row must not hold a unique value (such as a worn layer) a saved row now takes.
            var data = transaction.GetDataAccess<T>();

            foreach (var id in deletions)
            {
                await data.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            }

            foreach (var batch in changed.Chunk(SnapshotBatchSize))
            {
                await transaction.UpsertSnapshotsAsync(batch, cancellationToken).ConfigureAwait(false);
            }

            _pending = pending;
            Written = changed.Count;
        };
    }

    public void Committed()
    {
        if (_pending is { } pending)
        {
            _saved = pending;
            _pending = null;
        }

        var captured = _captured;
        _captured = [];

        if (_deletions is not null && captured.Count > 0)
        {
            _deletions.Committed(captured);
        }
    }
}
