namespace Moongate.Server.Core.Interfaces.Commands;

/// <summary>
///     Lets a command executor tell the console what its arguments can be, for TAB completion.
/// </summary>
/// <remarks>
///     Called on the console's thread, never on the game loop thread: give fixed lists or names read from disk, never
///     game state.
/// </remarks>
public interface ICommandArgumentCompleter
{
    /// <summary>
    ///     Gets the values the argument being typed can take, after <paramref name="previousArguments" />, the arguments
    ///     already typed before it; none when the command takes no fixed value there.
    /// </summary>
    IReadOnlyList<string> GetArgumentCompletions(IReadOnlyList<string> previousArguments);
}
