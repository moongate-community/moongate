using System.Net.Sockets;
using System.Text;
using Moongate.Console.Admin.Plugin.Types;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.Commands;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Types;
using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Console.Admin.Plugin.Services.Console;

/// <summary>
/// One admin-console connection: a banner, a login handshake against <see cref="IAccountService" />,
/// then a read-line REPL that dispatches commands to <see cref="ICommandService" /> on the game loop.
/// </summary>
public sealed class ConsoleSession
{
    private const int MaxLoginAttempts = 3;
    private const int MaxLineLength = 1024;
    private static readonly AccountLevelType MinLevel = AccountLevelType.GrandMaster;

    private readonly TcpClient _client;
    private readonly ICommandService _commands;
    private readonly IAccountService _accounts;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly object _writeLock = new();

    private StreamWriter? _writer;
    private volatile bool _closed;

    public ConsoleSession(
        TcpClient client,
        ICommandService commands,
        IAccountService accounts,
        IMainThreadDispatcher dispatcher
    )
    {
        _client = client;
        _commands = commands;
        _accounts = accounts;
        _dispatcher = dispatcher;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var stream = _client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            _writer = writer;

            WriteLine("Moongate admin console.");

            var level = await AuthenticateAsync(reader, ct);

            if (level is null)
            {
                return;
            }

            WriteLine("Authenticated. Type 'help' or 'quit'.");
            await ReplLoopAsync(reader, level.Value, ct);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // client dropped or the server is shutting down — nothing to report
        }
        finally
        {
            Close();
        }
    }

    private async Task<AccountLevelType?> AuthenticateAsync(StreamReader reader, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxLoginAttempts; attempt++)
        {
            WriteLine("login:");
            var username = await ReadLineAsync(reader, ct);
            WriteLine("password:");
            var password = await ReadLineAsync(reader, ct);

            if (username is null || password is null)
            {
                return null;
            }

            var auth = _accounts.Authenticate(username, password);
            var level = _accounts.GetByUsername(username)?.AccountLevel;

            switch (ConsoleAuth.Evaluate(auth, level, MinLevel))
            {
                case ConsoleAuthResultType.Allowed:
                    return level;

                case ConsoleAuthResultType.InsufficientPrivileges:
                    WriteLine("Insufficient privileges.");

                    return null;

                default:
                    WriteLine("Login failed.");

                    break;
            }
        }

        WriteLine("Too many attempts.");

        return null;
    }

    private async Task ReplLoopAsync(StreamReader reader, AccountLevelType level, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_closed)
        {
            Write("> ");
            var line = await ReadLineAsync(reader, ct);

            if (line is null)
            {
                return;
            }

            switch (ConsoleInput.Classify(line))
            {
                case ConsoleInputKind.Empty:
                    continue;

                case ConsoleInputKind.Quit:
                    WriteLine("Bye.");

                    return;

                case ConsoleInputKind.Help:
                    WriteHelp(level);

                    continue;

                default:
                    var invocation = new CommandInvocation(CommandSourceType.Console, level, null, line, WriteLine);
                    _dispatcher.Post(() => _commands.Execute(invocation));

                    continue;
            }
        }
    }

    private void WriteHelp(AccountLevelType level)
    {
        foreach (var command in _commands.ListCommands(CommandSourceType.Console))
        {
            if (level >= command.MinLevel)
            {
                WriteLine($"  {command.Name} - {command.Description}");
            }
        }

        WriteLine("  help - list commands");
        WriteLine("  quit - close the session");
    }

    private async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken ct)
    {
        var raw = await reader.ReadLineAsync(ct);

        if (raw is null)
        {
            return null;
        }

        if (raw.Length > MaxLineLength)
        {
            raw = raw[..MaxLineLength];
        }

        return TelnetInput.StripControls(raw);
    }

    private void Write(string text)
        => WriteRaw(text, newline: false);

    private void WriteLine(string text)
        => WriteRaw(text, newline: true);

    private void WriteRaw(string text, bool newline)
    {
        if (_closed)
        {
            return;
        }

        lock (_writeLock)
        {
            if (_closed || _writer is null)
            {
                return;
            }

            try
            {
                if (newline)
                {
                    _writer.WriteLine(text);
                }
                else
                {
                    _writer.Write(text);
                }

                _writer.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _closed = true;
            }
        }
    }

    public void Close()
    {
        _closed = true;

        try
        {
            _client.Close();
        }
        catch (Exception)
        {
            // socket already gone
        }
    }
}
