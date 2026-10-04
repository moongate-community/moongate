using System.Text;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Interfaces.Internal.Console;
using Moongate.Server.Services.Console.Internal;
using Serilog;

namespace Moongate.Server.Services.Console;

/// <summary>
///     Polls the terminal for keystrokes and dispatches submitted command lines. TAB completes the command name and the
///     arguments its command knows, Up and Down walk the lines submitted in this run.
/// </summary>
public sealed class ConsoleInputService : IConsoleInputService, IDisposable
{
    private const int PollDelayMilliseconds = 25;

    private readonly IConsolePromptService _prompt;
    private readonly ICommandSystemService _commands;
    private readonly IConsoleKeySource _keys;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConsoleHistory _history = new();
    private readonly ILogger _logger = Log.ForContext<ConsoleInputService>();

    private Task _loop = Task.CompletedTask;

    public ConsoleInputService(IConsolePromptService prompt, ICommandSystemService commands)
        : this(prompt, commands, new SystemConsoleKeySource())
    {
    }

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
    {
        return key.KeyChar == '\0' && key.Key == default && key.Modifiers == 0;
    }

    // A line carrying a password, account create <user> <password>: masked on the prompt and never kept.
    private static bool IsSensitive(string input)
    {
        return TryFindPassword(input, out _, out _);
    }

    private static string MaskSensitiveInput(string input)
    {
        return TryFindPassword(input, out var start, out var end)
            ? input[..start] + new string('*', end - start) + input[end..]
            : input;
    }

    private static bool TryFindPassword(string input, out int start, out int end)
    {
        var text = input.AsSpan();
        var position = 0;
        var command = ReadToken(text, ref position);
        var action = ReadToken(text, ref position);
        var username = ReadToken(text, ref position);
        start = end = 0;

        if (!command.Equals("account".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            !action.Equals("create".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            username.IsEmpty)
        {
            return false;
        }

        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        start = position;
        _ = ReadToken(text, ref position);
        end = position;

        return end > start;
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
        var tabbed = false;

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

                // A second TAB in a row lists the matches the first one could not choose between.
                var secondTab = tabbed && key.Key == ConsoleKey.Tab;
                tabbed = key.Key == ConsoleKey.Tab;

                if (key.Key == ConsoleKey.Tab)
                {
                    // A TAB that completed something starts over: only one that could not arms the listing.
                    tabbed = !Complete(buffer, secondTab);

                    continue;
                }

                if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow)
                {
                    var recalled = key.Key == ConsoleKey.UpArrow ? _history.Previous(buffer.ToString()) : _history.Next();

                    if (recalled is not null)
                    {
                        Replace(buffer, recalled);
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    var commandLine = buffer.ToString();
                    buffer.Clear();
                    _prompt.UpdateInput("");

                    // A line the prompt masks carries a password: it is never kept.
                    if (!IsSensitive(commandLine))
                    {
                        _history.Add(commandLine);
                    }
                    else
                    {
                        _history.Reset();
                    }

                    await SubmitAsync(commandLine, cancellationToken);

                    continue;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0)
                    {
                        buffer.Length--;
                        _prompt.UpdateInput(MaskSensitiveInput(buffer.ToString()));
                        _history.Reset();
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.Escape)
                {
                    buffer.Clear();
                    _prompt.UpdateInput("");

                    // A line thrown away, a password with it, does not come back with Down.
                    _history.Reset();

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    buffer.Append(key.KeyChar);
                    _prompt.UpdateInput(MaskSensitiveInput(buffer.ToString()));

                    // An edited line is the one being typed: the next Up keeps it for Down.
                    _history.Reset();
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

    // Whether the line changed.
    private bool Complete(StringBuilder buffer, bool list)
    {
        var (text, matches) = ConsoleCompletion.Complete(buffer.ToString(), CandidatesAfter);

        if (text != buffer.ToString())
        {
            Replace(buffer, text);
            _history.Reset();

            return true;
        }

        if (list && matches.Count > 1)
        {
            _prompt.WriteOutputLine(string.Join("  ", matches), CommandOutputLevel.Information);
        }

        return false;
    }

    // The console commands for the first word, then the values the command gives for its next argument.
    private IEnumerable<string> CandidatesAfter(IReadOnlyList<string> previous)
    {
        if (previous.Count == 0)
        {
            return _commands.GetRegisteredCommands()
                            .Where(definition => definition.Source.HasFlag(CommandSourceType.Console))
                            .SelectMany(definition => definition.Aliases);
        }

        return _commands.GetArgumentCompletions(previous[0], previous.Skip(1).ToArray());
    }

    private void Replace(StringBuilder buffer, string text)
    {
        buffer.Clear().Append(text);
        _prompt.UpdateInput(MaskSensitiveInput(text));
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
    {
        _lifetime.Dispose();
    }
}
