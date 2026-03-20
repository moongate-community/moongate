using Moongate.Server.Data.Internal.Commands;

namespace Moongate.Server.Interfaces.Services.Console;

/// <summary>
/// Defines a dependency-injected command executor.
/// </summary>
public interface ICommandExecutor
{
    /// <summary>
    /// Optional autocomplete provider for command arguments.
    /// </summary>
    Func<CommandAutocompleteContext, IReadOnlyList<string>>? AutocompleteProvider => null;

    /// <summary>
    /// Executes the command for the given context.
    /// </summary>
    /// <param name="context">Execution context.</param>
    Task ExecuteCommandAsync(CommandSystemContext context);
}
