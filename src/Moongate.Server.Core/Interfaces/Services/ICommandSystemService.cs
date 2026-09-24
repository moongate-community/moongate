using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Registers and dispatches operator commands.</summary>
public interface ICommandSystemService : IMoongateStartupService
{
    /// <summary>Parses and executes a raw command line, returning the lines its handler produced.</summary>
    /// <param name="commandLine">Raw command text including arguments.</param>
    /// <param name="source">Source that submitted the command.</param>
    /// <param name="session">Invoking session when the command came from a session, otherwise null.</param>
    /// <param name="cancellationToken">Cancels the invocation before the handler runs.</param>
    /// <returns>Output lines produced by the command, or by the rejection that replaced it.</returns>
    /// <remarks>
    /// <see cref="CommandSourceType.Console" /> resolves to <see cref="AccountType.Administrator" /> without
    /// authentication, and it is this parameter's default. Any caller that is not a trusted in-process
    /// console must pass an explicit source and authenticate the user first.
    /// </remarks>
    Task<IReadOnlyList<CommandOutputLine>> ExecuteAsync(
        string commandLine,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>Gets one definition per registered command, ordered by name.</summary>
    IReadOnlyList<CommandDefinition> GetRegisteredCommands();
}
