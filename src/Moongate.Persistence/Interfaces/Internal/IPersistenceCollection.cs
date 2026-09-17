using Moongate.Core.Primitives;

namespace Moongate.Persistence.Interfaces.Internal;

/// <summary>Coordinates the lifecycle of one typed persistence collection.</summary>
internal interface IPersistenceCollection : IAsyncDisposable
{
    /// <summary>Opens the raw store and validates all persisted typed payloads.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Serializes the configured live source without mutating the durable store.</summary>
    Dictionary<Serial, byte[]> Capture(CancellationToken cancellationToken = default);

    /// <summary>Durably upserts a previously captured set of serialized entities.</summary>
    Task CommitAsync(Dictionary<Serial, byte[]> payloads, CancellationToken cancellationToken = default);

    /// <summary>Publishes a checkpoint for the collection's committed state.</summary>
    Task CheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the collection without checkpointing its state.</summary>
    Task AbortAsync();

    /// <summary>Closes the collection after its owner has drained the shared mutation gate.</summary>
    Task CloseFromOwnerAsync();
}
