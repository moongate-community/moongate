using System.Threading.Channels;
using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Serilog;

namespace Moongate.Server.Services.Login.Internal;

internal sealed class LoginPacketMailbox : IAsyncDisposable
{
    private readonly Channel<IPacket> _queue;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly IReadOnlyDictionary<Type, Func<LoginSession, IPacket, CancellationToken, ValueTask>> _handlers;
    private readonly ILogger _logger;
    private Task? _worker;
    private Task? _stop;

    public LoginSession Session { get; }

    public LoginPacketMailbox(
        LoginSession session,
        IReadOnlyDictionary<Type, Func<LoginSession, IPacket, CancellationToken, ValueTask>> handlers,
        ILogger logger,
        int capacity
    )
    {
        Session = session;
        _handlers = handlers;
        _logger = logger;
        _queue = Channel.CreateBounded<IPacket>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait
            }
        );
    }

    public void Start()
    {
        _worker = Task.Run(RunAsync);
    }

    public bool TryWrite(IPacket packet)
    {
        return !_cancellation.IsCancellationRequested && _queue.Writer.TryWrite(packet);
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            return _stop ??= StopCoreAsync();
        }
    }

    private async Task StopCoreAsync()
    {
        _queue.Writer.TryComplete();
        await _cancellation.CancelAsync().ConfigureAwait(false);

        if (_worker is not null)
        {
            await _worker.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        _cancellation.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var packet in _queue.Reader.ReadAllAsync(_cancellation.Token))
            {
                if (!_handlers.TryGetValue(packet.GetType(), out var handler))
                {
                    continue;
                }

                await handler(Session, packet, _cancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.Error(exception, "Login packet handler failed for session {SessionId}", Session.SessionId);
            _queue.Writer.TryComplete(exception);

            if (Session.NetworkSession.Client is { } connection)
            {
                await connection.CloseAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return new(StopAsync());
    }
}
