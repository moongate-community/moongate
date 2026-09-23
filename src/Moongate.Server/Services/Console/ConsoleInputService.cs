using System.Text;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Interfaces.Internal.Console;
using Moongate.Server.Services.Console.Internal;
using Serilog;

namespace Moongate.Server.Services.Console;

/// <summary>Polls the terminal for keystrokes and dispatches submitted command lines.</summary>
public sealed class ConsoleInputService : IConsoleInputService, IDisposable
{
    private const int PollDelayMilliseconds = 25;

    private readonly IConsolePromptService _prompt;
    private readonly ICommandSystemService _commands;
    private readonly IConsoleKeySource _keys;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ILogger _logger = Log.ForContext<ConsoleInputService>();

    private Task _loop = Task.CompletedTask;

    public ConsoleInputService(IConsolePromptService prompt, ICommandSystemService commands)
        : this(prompt, commands, new SystemConsoleKeySource()) { }

    internal ConsoleInputService(
        IConsolePromptService prompt,
        ICommandSystemService commands,
        IConsoleKeySource keys
    )
    {
        _prompt = prompt;
        _commands = commands;
        _keys = keys;
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        if (!_prompt.IsInteractive)
        {
            _logger.Information("Interactive console prompt disabled (non-interactive terminal).");

            return Task.CompletedTask;
        }

        _prompt.LockInput();
        _prompt.ShowPrompt();
        _logger.Information(
            "Console input is locked. Press '{UnlockCharacter}' to unlock.",
            _prompt.UnlockCharacter
        );
        _loop = Task.Run(() => RunAsync(_lifetime.Token));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        await _lifetime.CancelAsync();

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }

        _prompt.HidePrompt();
    }

    private static bool IsNoKey(ConsoleKeyInfo key)
        => key.KeyChar == '\0' && key.Key == default && key.Modifiers == 0;

    private static string MaskSensitiveInput(string input)
    {
        var text = input.AsSpan();
        var position = 0;
        var command = ReadToken(text, ref position);
        var action = ReadToken(text, ref position);
        var username = ReadToken(text, ref position);

        if (!command.Equals("account".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            !action.Equals("create".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            username.IsEmpty)
        {
            return input;
        }

        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        var passwordStart = position;
        _ = ReadToken(text, ref position);

        return position == passwordStart
                   ? input
                   : input[..passwordStart] + new string('*', position - passwordStart) + input[position..];
    }

    private static ReadOnlySpan<char> ReadToken(ReadOnlySpan<char> text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        var start = position;

        while (position < text.Length && !char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        return text[start..position];
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        var lockWarningShown = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var hasKey = _keys.KeyAvailable;
                var key = default(ConsoleKeyInfo);

                if (hasKey)
                {
                    key = _keys.ReadKey();
                    hasKey = !IsNoKey(key);
                }

                if (!hasKey)
                {
                    try
                    {
                        await Task.Delay(PollDelayMilliseconds, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    continue;
                }

                if (_prompt.IsInputLocked)
                {
                    if (key.KeyChar == _prompt.UnlockCharacter)
                    {
                        _prompt.UnlockInput();
                        lockWarningShown = false;
                    }
                    else if (!lockWarningShown)
                    {
                        _logger.Warning(
                            "Console input is locked. Press '{UnlockCharacter}' to unlock.",
                            _prompt.UnlockCharacter
                        );
                        lockWarningShown = true;
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    var commandLine = buffer.ToString();
                    buffer.Clear();
                    _prompt.UpdateInput("");
                    await SubmitAsync(commandLine, cancellationToken);

                    continue;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        _prompt.UpdateInput(MaskSensitiveInput(buffer.ToString()));
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.Escape)
                {
                    buffer.Clear();
                    _prompt.UpdateInput("");

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Append(key.KeyChar);
                    _prompt.UpdateInput(MaskSensitiveInput(buffer.ToString()));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal cancellation exit
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Console input has stopped unexpectedly; no further keys will be processed.");
            _prompt.HidePrompt();
        }
    }

    private async Task SubmitAsync(string commandLine, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return;
        }

        try
        {
            var output = await _commands.ExecuteAsync(
                             commandLine,
                             CommandSourceType.Console,
                             null,
                             cancellationToken
                         );

            foreach (var line in output)
            {
                _prompt.WriteOutputLine(line.Text, line.Level);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown cancelled the command
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Console command execution failed");
            _prompt.WriteOutputLine("Command failed. Check logs for details.", CommandOutputLevel.Error);
        }
    }

    public void Dispose()
        => _lifetime.Dispose();
}
