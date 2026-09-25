namespace Moongate.Server.Core.Interfaces.Persistence;

/// <summary>
///     Excludes world captures while a critical database operation applies its result to the owner loop.
/// </summary>
public interface IPersistenceOperationBarrier
{
    /// <summary>
    ///     Runs an operation through its awaited post-commit owner application before admitting another operation or save.
    /// </summary>
    /// <remarks>
    ///     Acquire this barrier before calling persistence. Do not call it from the owner loop or reenter it.
    ///     Any admitted callback failure or cancellation faults future operations and world captures until the host restarts;
    ///     the barrier cannot know whether the database committed or the owner state was applied. Callbacks must await all work.
    /// </remarks>
    /// <param name="operation">
    ///     The database operation and its awaited owner-loop application.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancellation before admission is harmless; cancellation after admission faults coordination.
    /// </param>
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
