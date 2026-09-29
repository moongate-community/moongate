using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Interfaces.Internal;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceEntityRegistration<T> : IPersistenceEntityRegistration where T : class, IMoongateEntity
{
    private const int SnapshotBatchSize = 256;
    private readonly Func<IEnumerable<T>> _source;
    private readonly Func<T, T> _snapshot;
    private readonly IPersistenceDeletionSource? _deletions;
    private IReadOnlyCollection<Serial> _captured = [];

    public Type EntityType => typeof(T);

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

        return async (transaction, cancellationToken) =>
        {
            // Deletions first: a deleted row must not hold a unique value (such as a worn layer) a saved row now takes.
            var data = transaction.GetDataAccess<T>();

            foreach (var id in deletions)
            {
                await data.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            }

            foreach (var batch in values.Chunk(SnapshotBatchSize))
            {
                await transaction.UpsertSnapshotsAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        };
    }

    public void Committed()
    {
        var captured = _captured;
        _captured = [];

        if (_deletions is not null && captured.Count > 0)
        {
            _deletions.Committed(captured);
        }
    }
}
