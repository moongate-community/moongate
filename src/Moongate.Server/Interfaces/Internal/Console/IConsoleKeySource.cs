namespace Moongate.Server.Interfaces.Internal.Console;

/// <summary>
///     Keyboard reads the input loop needs, isolated so key handling can be tested.
/// </summary>
internal interface IConsoleKeySource
{
    /// <summary>
    ///     Gets whether a key press is ready to read without blocking.
    /// </summary>
    bool KeyAvailable { get; }

    /// <summary>
    ///     Reads the next available key press.
    /// </summary>
    /// <returns>
    ///     The key and its modifier state.
    /// </returns>
    ConsoleKeyInfo ReadKey();
}
