namespace Moongate.Server.Data.Internal.Commands;

/// <summary>
/// Provides command-line autocomplete context for command-specific argument completion providers.
/// </summary>
public sealed class CommandAutocompleteContext
{
    /// <summary>
    /// Gets the command token used in the current input line.
    /// </summary>
    public required string CommandName { get; init; }

    /// <summary>
    /// Gets the parsed argument tokens currently present in the input line.
    /// </summary>
    public required string[] Arguments { get; init; }

    /// <summary>
    /// Gets whether the input line currently ends with whitespace.
    /// </summary>
    public required bool EndsWithWhitespace { get; init; }
}
