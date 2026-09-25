using Moongate.Server.Core.Data.Commands;

namespace Moongate.Server.Core.Interfaces.Commands;

/// <summary>
///     Executes one registered command.
/// </summary>
/// <remarks>
///     Handlers run on the thread that called the command system, never on the game loop thread.
///     A command that mutates game state must post its own work item to IGameLoopService.
/// </remarks>
public interface ICommandExecutor
{
    /// <summary>
    ///     Runs the command, writing any output into the supplied context.
    /// </summary>
    /// <param name="context">
    ///     Parsed invocation and output sink.
    /// </param>
    Task ExecuteAsync(CommandContext context);
}
