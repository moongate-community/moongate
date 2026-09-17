using System.Threading.Channels;
using Moongate.Network.Client;
using Serilog;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class SessionPacketOutbox
{
    private readonly MoongateTcpClient _client;
    private readonly Channel<byte[]> _queue;
    private readonly Action<long> _retire;
    private readonly ILogger _logger = Log.ForContext<SessionPacketOutbox>();

    public Task Completion { get; private set; } = Task.CompletedTask;

    public SessionPacketOutbox(MoongateTcpClient client, int capacity, Action<long> retire)
    {
        _client = client;
        _retire = retire;
        _queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public void Start()
    {
        // Transport middleware can block before its first await. Start the entire drain off-loop.
        Completion = Task.Run(RunAsync);
    }

    public bool TryWrite(byte[] frame)
    {
        return _queue.Writer.TryWrite(frame);
    }

    public void Close()
    {
        _queue.Writer.TryComplete();
        _client.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            var drain = DrainAsync();
            // A peer can disconnect while this outbox is idle and no further packet is sent.
            await Task.WhenAny(drain, _client.Completion).ConfigureAwait(false);
            Close();
            try
            {
                await drain.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!_client.IsConnected)
            {
                // Closing the transport cancels any admitted send.
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Outgoing packet send failed for session {SessionId}", _client.SessionId);
            }

            try
            {
                await _client.Completion.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Outgoing connection cleanup failed for session {SessionId}", _client.SessionId);
            }
        }
        finally
        {
            _retire(_client.SessionId);
        }
    }

    private async Task DrainAsync()
    {
        await foreach (var frame in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (!_client.IsConnected)
            {
                break;
            }

            await _client.SendAsync(frame, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
