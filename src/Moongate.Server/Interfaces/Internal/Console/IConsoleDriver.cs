namespace Moongate.Server.Interfaces.Internal.Console;

/// <summary>Terminal operations the prompt needs, isolated so rendering can be tested.</summary>
internal interface IConsoleDriver
{
    int WindowWidth { get; }

    int WindowHeight { get; }

    int WindowTop { get; }

    int BufferHeight { get; }

    ConsoleColor ForegroundColor { get; set; }

    void ResetColor();

    void SetCursorPosition(int left, int top);

    void Write(string value);

    void WriteLine(string value);
}
