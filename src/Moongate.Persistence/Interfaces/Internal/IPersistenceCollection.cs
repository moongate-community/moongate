namespace Moongate.Persistence.Interfaces.Internal;

/// <summary>Coordinates the lifecycle of one typed persistence collection.</summary>
internal interface IPersistenceCollection : IAsyncDisposable
{
    /// <summary>Opens the raw store and validates all persisted typed payloads.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Publishes a checkpoint for the collection's committed state.</summary>
    Task CheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>Closes the collection without checkpointing its state.</summary>
    Task AbortAsync();
}
