using Moongate.Server.Interfaces.Internal.Console;

namespace Moongate.Tests.TestSupport.Console;

internal sealed class RecordingConsoleDriver : IConsoleDriver
{
    private readonly List<string> _operations = [];

    private ConsoleColor _foregroundColor = ConsoleColor.Gray;

    public int WindowWidth { get; set; } = 80;

    public int WindowHeight { get; set; } = 24;

    public int WindowTop { get; set; }

    public int BufferHeight { get; set; } = 24;

    public ConsoleColor ForegroundColor
    {
        get => _foregroundColor;
        set
        {
            _foregroundColor = value;
            Record($"color:{value}");
        }
    }

    /// <summary>
    ///     Index of the operation that should throw an IOException, or -1 to never throw.
    /// </summary>
    public int ThrowOnOperation { get; set; } = -1;

    public IReadOnlyList<string> Operations => _operations;

    public void ResetColor()
    {
        Record("reset");
    }

    public void SetCursorPosition(int left, int top)
    {
        Record($"pos:{left},{top}");
    }

    public void Write(string value)
    {
        Record($"write:{value}");
    }

    public void WriteLine(string value)
    {
        Record($"writeline:{value}");
    }

    private void Record(string operation)
    {
        if (_operations.Count == ThrowOnOperation)
        {
            throw new IOException("console unavailable");
        }

        _operations.Add(operation);
    }
}
