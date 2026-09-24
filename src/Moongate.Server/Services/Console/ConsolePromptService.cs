using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Interfaces.Internal.Console;
using Moongate.Server.Services.Console.Internal;

namespace Moongate.Server.Services.Console;

/// <summary>Keeps a command prompt pinned to the last terminal row and serialises writes against it.</summary>
public sealed class ConsolePromptService : IConsolePromptService
{
    private const string PromptPrefix = "MG> ";
    private const string LockedPromptPrefix = "MG [LOCKED]> ";
    private const char PromptUnlockCharacter = '*';

    private readonly Lock _sync = new();
    private readonly IConsoleDriver _driver;

    private string _input = "";
    private bool _interactive;
    private bool _locked = true;
    private bool _promptVisible;

    /// <inheritdoc />
    public bool IsInteractive
    {
        get
        {
            lock (_sync)
            {
                return _interactive;
            }
        }
    }

    /// <inheritdoc />
    public bool IsInputLocked
    {
        get
        {
            lock (_sync)
            {
                return _locked;
            }
        }
    }

    /// <inheritdoc />
    public char UnlockCharacter => PromptUnlockCharacter;

    public ConsolePromptService()
        : this(new SystemConsoleDriver(), IsInteractiveTerminal()) { }

    internal ConsolePromptService(IConsoleDriver driver, bool interactive)
    {
        _driver = driver;
        _interactive = interactive;
    }

    /// <inheritdoc />
    public void HidePrompt()
    {
        lock (_sync)
        {
            if (!_interactive || !_promptVisible)
            {
                return;
            }

            _promptVisible = false;

            try
            {
                ClearPromptRow();
            }
            catch (Exception exception) when (exception is IOException or ArgumentOutOfRangeException)
            {
                Degrade();
            }
        }
    }

    /// <inheritdoc />
    public void LockInput()
    {
        lock (_sync)
        {
            _locked = true;
            _input = "";
            SafeRender();
        }
    }

    /// <inheritdoc />
    public void RunWithPromptHidden(Action write)
    {
        lock (_sync)
        {
            if (!_interactive)
            {
                write();

                return;
            }

            var written = false;

            try
            {
                if (_promptVisible)
                {
                    ClearPromptRow();
                }

                written = true;
                write();

                if (_promptVisible)
                {
                    RenderPrompt();
                }
            }
            catch (Exception exception) when (exception is IOException or ArgumentOutOfRangeException)
            {
                Degrade();

                if (!written)
                {
                    write();
                }
            }
        }
    }

    /// <inheritdoc />
    public void ShowPrompt()
    {
        lock (_sync)
        {
            if (!_interactive || _promptVisible)
            {
                return;
            }

            _promptVisible = true;
            SafeRender();
        }
    }

    /// <inheritdoc />
    public void UnlockInput()
    {
        lock (_sync)
        {
            _locked = false;
            SafeRender();
        }
    }

    /// <inheritdoc />
    public void UpdateInput(string input)
    {
        lock (_sync)
        {
            _input = input;
            SafeRender();
        }
    }

    /// <inheritdoc />
    public void WriteOutputLine(string text, CommandOutputLevel level)
        => RunWithPromptHidden(
            () =>
            {
                if (level == CommandOutputLevel.Information)
                {
                    _driver.WriteLine(text);

                    return;
                }

                _driver.ForegroundColor = level == CommandOutputLevel.Error ? ConsoleColor.Red : ConsoleColor.Yellow;
                _driver.WriteLine(text);
                _driver.ResetColor();
            }
        );

    private void ClearPromptRow()
    {
        var width = _driver.WindowWidth;
        var row = GetPromptRow();

        EraseRow(row, width);
    }

    private void Degrade()
    {
        _interactive = false;
        _promptVisible = false;
    }

    private void EraseRow(int row, int width)
    {
        var eraseWidth = Math.Max(1, width - 1);

        _driver.SetCursorPosition(0, row);
        _driver.Write(new(' ', eraseWidth));
        _driver.SetCursorPosition(0, row);
    }

    private int GetPromptRow()
        => Math.Clamp(_driver.WindowTop + _driver.WindowHeight - 1, 0, _driver.BufferHeight - 1);

    private static bool IsInteractiveTerminal()
    {
        if (!Environment.UserInteractive)
        {
            return false;
        }

        return !System.Console.IsInputRedirected && !System.Console.IsOutputRedirected;
    }

    private void RenderPrompt()
    {
        var width = _driver.WindowWidth;
        var row = GetPromptRow();
        var prefix = _locked ? LockedPromptPrefix : PromptPrefix;
        var line = prefix + _input;

        // Stay one cell short of the full width: writing the final column wraps
        // immediately on Windows conhost without VT processing, which scrolls the
        // viewport and drags the pinned prompt row off-screen.
        var renderWidth = Math.Max(1, width - 1);

        EraseRow(row, width);
        _driver.Write(line.Length > renderWidth ? line[..renderWidth] : line);
        _driver.SetCursorPosition(Math.Min(width - 1, line.Length), row);
    }

    private void SafeRender()
    {
        if (!_interactive || !_promptVisible)
        {
            return;
        }

        try
        {
            RenderPrompt();
        }
        catch (Exception exception) when (exception is IOException or ArgumentOutOfRangeException)
        {
            Degrade();
        }
    }
}
