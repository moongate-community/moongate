using Moongate.Server.Interfaces.Internal.Console;

namespace Moongate.Server.Services.Console.Internal;

/// <summary>Reads keys from the real terminal without echoing them.</summary>
internal sealed class SystemConsoleKeySource : IConsoleKeySource
{
    public bool KeyAvailable => System.Console.KeyAvailable;

    public ConsoleKeyInfo ReadKey()
    {
        return System.Console.ReadKey(intercept: true);
    }
}
