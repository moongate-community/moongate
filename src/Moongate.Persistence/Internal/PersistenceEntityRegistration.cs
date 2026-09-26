using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces.Internal;

namespace Moongate.Persistence.Internal;

internal sealed class PersistenceEntityRegistration<T> : IPersistenceEntityRegistration where T : class, IMoongateEntity
{
    private const int SnapshotBatchSize = 256;
    private readonly Func<IEnumerable<T>> _source;
    private readonly Func<T, T> _snapshot;

    public Type EntityType => typeof(T);

    public PersistenceEntityRegistration(Func<IEnumerable<T>> source, Func<T, T> snapshot)
    {
        _source = source;
        _snapshot = snapshot;
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

        return async (transaction, cancellationToken) =>
        {
            foreach (var batch in values.Chunk(SnapshotBatchSize))
            {
                await transaction.UpsertSnapshotsAsync(batch, cancellationToken).ConfigureAwait(false);
            }
        };
    }
}
