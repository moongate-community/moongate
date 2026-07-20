using System.Net;
using System.Net.Sockets;
using Moongate.Console.Admin.Plugin.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Threading;

namespace Moongate.Console.Admin.Plugin.Services.Hosting;

/// <summary>
/// Runs the line-based admin console. Owns a <see cref="TcpListener" /> and its lifetime, exactly as
/// the HTTP plugin owns Kestrel: drop the plugin and the game server boots with no console at all. A
/// bind failure is logged and swallowed — the console is optional and must never take the game down.
/// </summary>
public sealed class ConsoleServerService : ISquidStdService, IDisposable
{
    private readonly ILogger _logger = Log.ForContext<ConsoleServerService>();
    private readonly MoongateConsoleConfig _config;
    private readonly ICommandService _commands;
    private readonly IAccountService _accounts;
    private readonly IMainThreadDispatcher _dispatcher;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public ConsoleServerService(
        MoongateConsoleConfig config,
        ICommandService commands,
        IAccountService accounts,
        IMainThreadDispatcher dispatcher
    )
    {
        _config = config;
        _commands = commands;
        _accounts = accounts;
        _dispatcher = dispatcher;
    }

    /// <summary>The port actually bound — the OS-assigned one when the configured port is 0. Zero when disabled.</summary>
    public int BoundPort { get; private set; }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.Information("Admin console disabled (console.Enabled = false)");

            return ValueTask.CompletedTask;
        }

        try
        {
            _listener = new TcpListener(IPAddress.Parse(_config.Address), _config.Port);
            _listener.Start();
        }
        catch (Exception ex) when (ex is SocketException or FormatException)
        {
            _logger.Error(ex, "Admin console failed to bind {Address}:{Port}; console unavailable", _config.Address, _config.Port);
            _listener = null;

            return ValueTask.CompletedTask;
        }

        BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new();
        _acceptLoop = AcceptLoopAsync(_cts.Token);

        _logger.Information("Admin console listening on {Address}:{Port}", _config.Address, BoundPort);

        return ValueTask.CompletedTask;
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);

                // Task 5 replaces this with a ConsoleSession; the lifecycle just refuses connections for now.
                client.Close();
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is null)
        {
            return;
        }

        _cts?.Cancel();
        _listener.Stop();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (Exception)
            {
                // accept loop unwinding on cancellation — nothing to report
            }
        }

        _listener = null;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _listener?.Dispose();
    }
}
