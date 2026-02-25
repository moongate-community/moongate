using Moongate.Server.Data.Session;
using Moongate.Server.Data.Events.Base;
using Moongate.Server.Types.Commands;

namespace Moongate.Server.Data.Events.Console;

/// <summary>
/// Event emitted when a console command is submitted by an operator.
/// </summary>
/// <param name="CommandText">Command text entered in the server console.</param>
public readonly record struct CommandEnteredEvent(
    GameEventBase BaseEvent,
    string CommandText,
    CommandSourceType Source = CommandSourceType.Console,
    GameSession? GameSession = null
) : IGameEvent
{
    /// <summary>
    /// Creates a command-entered event with current timestamp.
    /// </summary>
    public CommandEnteredEvent(
        string commandText,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? gameSession = null
    )
        : this(GameEventBase.CreateNow(), commandText, source, gameSession) { }

    /// <summary>
    /// Creates a command-entered event with explicit timestamp.
    /// </summary>
    public CommandEnteredEvent(
        string commandText,
        long timestamp,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? gameSession = null
    )
        : this(new GameEventBase(timestamp), commandText, source, gameSession) { }
}
