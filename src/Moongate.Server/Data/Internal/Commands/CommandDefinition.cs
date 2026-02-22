using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Commands;

/// <summary>
/// Represents a registered command entry including aliases target, execution callback, and authorization metadata.
/// </summary>
public sealed class CommandDefinition
{
    /// <summary>
    /// Gets the normalized primary command name used for display and help output.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the help description shown by command listing output.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the asynchronous handler executed when the command is dispatched.
    /// </summary>
    public required Func<CommandSystemContext, Task> Handler { get; init; }

    /// <summary>
    /// Gets the allowed command source flags (console, in-game, or both).
    /// </summary>
    public CommandSourceType Source { get; init; }

    /// <summary>
    /// Gets the minimum account type required to execute the command.
    /// </summary>
    public AccountType MinimumAccountType { get; init; }
}
