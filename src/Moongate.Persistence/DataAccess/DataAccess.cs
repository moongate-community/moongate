using MemoryPack;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Interfaces.Internal;
using Moongate.Persistence.Internal;
using ZLinq;

namespace Moongate.Persistence.DataAccess;

public sealed class DataAccess<T> : IDataAccess<T>, IPersistenceCollection where T : class, IMoongateEntity
{
    private readonly BinaryCollectionStore _store;
    private readonly PersistenceMutationGate _mutationGate;
    private readonly Func<IEnumerable<T>>? _entitySource;
    private readonly bool _ownsMutationGate;

    internal DataAccess(BinaryCollectionStore store, Func<IEnumerable<T>>? entitySource = null)
        : this(store, new PersistenceMutationGate(), entitySource)
    {
        _ownsMutationGate = true;
    }

    internal DataAccess(
        BinaryCollectionStore store,
        PersistenceMutationGate mutationGate,
        Func<IEnumerable<T>>? entitySource = null
    )
    {
        _store = store;
        _mutationGate = mutationGate;
        _entitySource = entitySource;
    }

    public T? GetById(Serial id)
    {
        ValidateId(id);
        var payload = _store.Get(id);

        return payload is null ? null : DeserializeDetached(payload);
    }

    public IReadOnlyList<T> GetAll()
    {
        return DeserializeAll(_store.Capture());
    }

    public async Task<IReadOnlyList<T>> QueryAsync(Func<T, bool> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var snapshot = await _store.CaptureAsync(cancellationToken).ConfigureAwait(false);

        return FilterSnapshot(snapshot, predicate, cancellationToken);
    }

    public Task UpsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ValidateId(entity.Id);
        var id = entity.Id;
        var payload = MemoryPackSerializer.Serialize(entity);

        return _mutationGate.RunAsync(
            token => _store.UpsertAsync(id, payload, token),
            cancellationToken
        );
    }

    public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        ValidateId(id);

        return _mutationGate.RunAsync(
            token => _store.DeleteAsync(id, token),
            cancellationToken
        );
    }

    Task IPersistenceCollection.InitializeAsync(CancellationToken cancellationToken)
    {
        return InitializeAsync(cancellationToken);
    }

    Task IPersistenceCollection.CheckpointAsync(CancellationToken cancellationToken)
    {
        return _store.CheckpointAsync(cancellationToken);
    }

    Dictionary<Serial, byte[]> IPersistenceCollection.Capture(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_entitySource is null)
        {
            return [];
        }

        var entities = _entitySource() ??
                       throw new InvalidOperationException($"The live source for {typeof(T).FullName} returned null.");
        Dictionary<Serial, byte[]> captured = [];
        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(entity);
            var id = entity.Id;
            ValidateId(id);
            if (!captured.TryAdd(id, MemoryPackSerializer.Serialize(entity)))
            {
                throw new InvalidOperationException($"The live source for {typeof(T).FullName} contains duplicate identity {id}.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return captured;
    }

    async Task IPersistenceCollection.CommitAsync(
        Dictionary<Serial, byte[]> payloads,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(payloads);
        foreach (var (id, payload) in payloads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _store.UpsertAsync(id, payload, cancellationToken).ConfigureAwait(false);
        }
    }

    Task IPersistenceCollection.AbortAsync()
    {
        return _store.AbortAsync();
    }

    private static T DeserializeDetached(byte[] payload)
    {
        return MemoryPackSerializer.Deserialize<T>(payload) ??
               throw new InvalidDataException($"A persisted {typeof(T).FullName} payload was null.");
    }

    private static T[] DeserializeAll(byte[][] snapshot)
    {
        return snapshot.AsValueEnumerable()
                       .Select(DeserializeDetached)
                       .ToArray();
    }

    private static T[] FilterSnapshot(byte[][] snapshot, Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        var result = snapshot.AsValueEnumerable()
                             .Select(DeserializeDetached)
                             .Where(
                                 entity =>
                                 {
                                     cancellationToken.ThrowIfCancellationRequested();

                                     return predicate(entity);
                                 }
                             )
                             .ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        return result;
    }

    private static void ValidateId(Serial id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Serial must be nonzero.");
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _store.InitializeAsync(cancellationToken).ConfigureAwait(false);
        ValidatePersistedEntries(_store.CaptureEntries());
    }

    private static void ValidatePersistedEntries(KeyValuePair<Serial, byte[]>[] entries)
    {
        foreach (var entry in entries)
        {
            var entity = DeserializeDetached(entry.Value);

            if (entity.Id != entry.Key)
            {
                throw new InvalidDataException(
                    $"Persisted {typeof(T).FullName} identity {entity.Id} does not match collection key {entry.Key}."
                );
            }
        }
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        if (!_ownsMutationGate)
        {
            return _store.DisposeAsync();
        }

        return new ValueTask(
            _mutationGate.CloseAsync(() => _store.DisposeAsync().AsTask())
        );
    }
}
