namespace Moongate.Server.Core.Interfaces.GameLoop;

/// <summary>
///     A synchronous unit of work executed exclusively on the game loop thread.
/// </summary>
/// <remarks>
///     Exceptions are fatal to the loop. Handlers must not block waiting for work posted to the same loop.
/// </remarks>
public interface IGameLoopWorkItem
{
    /// <summary>
    ///     Executes the work to completion on the game loop thread.
    /// </summary>
    void Execute();
}
