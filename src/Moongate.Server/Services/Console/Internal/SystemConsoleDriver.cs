using Moongate.Server.Interfaces.Internal.Console;

namespace Moongate.Server.Services.Console.Internal;

/// <summary>Drives the real terminal through <see cref="System.Console" />.</summary>
internal sealed class SystemConsoleDriver : IConsoleDriver
{
    public int WindowWidth => Math.Max(1, System.Console.WindowWidth);

    public int WindowHeight => Math.Max(1, System.Console.WindowHeight);

    public int WindowTop => System.Console.WindowTop;

    public int BufferHeight => Math.Max(1, System.Console.BufferHeight);

    public ConsoleColor ForegroundColor
    {
        get => System.Console.ForegroundColor;
        set => System.Console.ForegroundColor = value;
    }

    public void ResetColor()
        => System.Console.ResetColor();

    public void SetCursorPosition(int left, int top)
        => System.Console.SetCursorPosition(left, top);

    public void Write(string value)
        => System.Console.Write(value);

    public void WriteLine(string value)
        => System.Console.WriteLine(value);
}
