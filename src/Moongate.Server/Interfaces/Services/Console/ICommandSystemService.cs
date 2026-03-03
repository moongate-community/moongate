using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Session;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Interfaces.Services.Console;

/// <summary>
/// Registers and dispatches operator commands.
/// </summary>
public interface ICommandSystemService : IMoongateService
{
    /// <summary>
    /// Executes a raw command text.
    /// </summary>
    /// <param name="commandWithArgs">Raw command text including arguments.</param>
    /// <param name="source">Command source.</param>
    /// <param name="session"> If comes from in game, is not null</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteCommandAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes a command and collects output lines produced by the command context.
    /// </summary>
    /// <param name="commandWithArgs">Raw command text including arguments.</param>
    /// <param name="source">Command source.</param>
    /// <param name="session">If command comes from in-game, associated session context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collected command output lines.</returns>
    Task<IReadOnlyList<string>> ExecuteCommandWithOutputAsync(
        string commandWithArgs,
        CommandSourceType source = CommandSourceType.Console,
        GameSession? session = null,
        CancellationToken cancellationToken = default
    )
        => throw new NotSupportedException("Command output capture is not supported by this implementation.");

    /// <summary>
    /// Gets autocomplete suggestions for the current command line.
    /// </summary>
    /// <param name="commandWithArgs">Current command line.</param>
    /// <returns>Replacement suggestions for the input line.</returns>
    IReadOnlyList<string> GetAutocompleteSuggestions(string commandWithArgs);

    /// <summary>
    /// Gets registered command definitions.
    /// </summary>
    /// <returns>Registered command definitions.</returns>
    IReadOnlyList<CommandDefinition> GetRegisteredCommands()
        => throw new NotSupportedException("Command definition listing is not supported by this implementation.");

    /// <summary>
    /// Registers one command or multiple aliases separated by <c>|</c>.
    /// </summary>
    /// <param name="commandName">Primary command name or aliases list.</param>
    /// <param name="handler">Command handler delegate.</param>
    /// <param name="description">Command help description.</param>
    /// <param name="source">Allowed command source.</param>
    /// <param name="minimumAccountType">Minimum account type required to run the command.</param>
    void RegisterCommand(
        string commandName,
        Func<CommandSystemContext, Task> handler,
        string description = "",
        CommandSourceType source = CommandSourceType.Console,
        AccountType minimumAccountType = AccountType.Administrator,
        Func<CommandAutocompleteContext, IReadOnlyList<string>>? autocompleteProvider = null
    );
}
