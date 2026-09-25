namespace Moongate.Server.Interfaces.Internal.Console;

/// <summary>
///     Keyboard reads the input loop needs, isolated so key handling can be tested.
/// </summary>
internal interface IConsoleKeySource
{
    bool KeyAvailable { get; }

    ConsoleKeyInfo ReadKey();
}
