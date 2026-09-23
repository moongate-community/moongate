namespace Moongate.Server.Interfaces.Internal.Console;

/// <summary>Terminal operations the prompt needs, isolated so rendering can be tested.</summary>
internal interface IConsoleDriver
{
    /// <summary>Gets the visible console width in character cells.</summary>
    int WindowWidth { get; }

    /// <summary>Gets the visible console height in character cells.</summary>
    int WindowHeight { get; }

    /// <summary>Gets the top row of the visible console window.</summary>
    int WindowTop { get; }

    /// <summary>Gets the total console buffer height in rows.</summary>
    int BufferHeight { get; }

    /// <summary>Gets or sets the foreground color used for subsequent output.</summary>
    ConsoleColor ForegroundColor { get; set; }

    /// <summary>Restores the console's default foreground and background colors.</summary>
    void ResetColor();

    /// <summary>Moves the cursor to the specified buffer coordinates.</summary>
    /// <param name="left">The zero-based column.</param>
    /// <param name="top">The zero-based row.</param>
    void SetCursorPosition(int left, int top);

    /// <summary>Writes text without appending a line terminator.</summary>
    /// <param name="value">The text to write.</param>
    void Write(string value);

    /// <summary>Writes text followed by a line terminator.</summary>
    /// <param name="value">The text to write.</param>
    void WriteLine(string value);
}
