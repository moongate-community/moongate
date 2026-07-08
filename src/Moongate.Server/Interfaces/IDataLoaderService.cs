namespace Moongate.Server.Interfaces;

/// <summary>
/// Runs every registered <see cref="IDataLoader" /> once at startup, in priority order.
/// </summary>
public interface IDataLoaderService
{
    /// <summary>Executes all registered loaders in order; the first loader that throws fails startup.</summary>
    ValueTask ExecuteLoadersAsync(CancellationToken ct = default);
}
