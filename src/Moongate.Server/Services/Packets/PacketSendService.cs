using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Packets.Internal;
using Serilog;

namespace Moongate.Server.Services.Packets;

/// <summary>
///     Snapshots outgoing packets and sends them through bounded, game-independent connection queues.
/// </summary>
public sealed class PacketSendService : IPacketSendService, ILoginPacketSendService
{
    private readonly Lock _gate = new();
    private readonly IConnectionService _connections;
    private readonly int _capacity;
    private readonly Dictionary<long, SessionPacketOutbox> _outboxes = new();
    private readonly Dictionary<long, Task> _cleanups = new();
    private readonly List<Exception> _failures = [];
    private readonly ILogger _logger = Log.ForContext<PacketSendService>();
    private bool _running;
    private bool _stopped;
    private Task? _stopTask;

    internal int ActiveOutboxCount
    {
        get
        {
            lock (_gate)
            {
                return _outboxes.Count;
            }
        }
    }

    public PacketSendService(IConnectionService connections, int capacity = 128)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _connections = connections;
        _capacity = capacity;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(long sessionId)
    {
        lock (_gate)
        {
            if (_outboxes.TryGetValue(sessionId, out var outbox))
            {
                outbox.Close();

                return outbox.Completion;
            }

            var cleanup = _connections.DisconnectAsync(sessionId);

            if (!_cleanups.ContainsKey(sessionId) && !cleanup.IsCompletedSuccessfully)
            {
                _cleanups.Add(sessionId, ObserveCleanupAsync(sessionId, cleanup, null));
            }

            return cleanup;
        }
    }

    public Task DisconnectAsync(long sessionId, INetworkConnection expectedConnection)
    {
        ArgumentNullException.ThrowIfNull(expectedConnection);

        lock (_gate)
        {
            if (_outboxes.TryGetValue(sessionId, out var outbox) &&
                ReferenceEquals(outbox.Connection, expectedConnection))
            {
                outbox.Close();

                return outbox.Completion;
            }
        }

        return _connections.DisconnectAsync(sessionId, expectedConnection);
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                throw new InvalidOperationException("The packet sender cannot restart after shutdown.");
            }

            _running = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            _running = false;
            _stopped = true;

            foreach (var outbox in _outboxes.Values)
            {
                outbox.Close();
            }

            return _stopTask ??= StopCoreAsync();
        }
    }

    /// <inheritdoc />
    public bool TrySend(long sessionId, IOutgoingPacket packet)
    {
        return TrySendCore(sessionId, packet, null);
    }

    /// <inheritdoc />
    public bool TrySend(long sessionId, INetworkConnection expectedConnection, IOutgoingPacket packet)
    {
        ArgumentNullException.ThrowIfNull(expectedConnection);

        return TrySendCore(sessionId, packet, expectedConnection);
    }

    /// <inheritdoc />
    public Task<bool> SendAndDisconnectAsync(
        long sessionId,
        INetworkConnection expectedConnection,
        IOutgoingPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(expectedConnection);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_running ||
                !_connections.TryGet(sessionId, out var connection, out var disconnectRequested) ||
                !ReferenceEquals(connection, expectedConnection))
            {
                return Task.FromResult(false);
            }

            if (!_outboxes.TryGetValue(sessionId, out var outbox))
            {
                outbox = new(
                    connection,
                    disconnectRequested,
                    _capacity,
                    id => _connections.DisconnectAsync(id, connection)
                );
                _outboxes.Add(sessionId, outbox);
                outbox.Start();
                _cleanups.Add(sessionId, ObserveCleanupAsync(sessionId, outbox.Completion, outbox));
            }
            else if (!ReferenceEquals(outbox.Connection, connection))
            {
                return Task.FromResult(false);
            }

            try
            {
                if (outbox.TryWriteTerminal(PacketCodec.Encode(packet)))
                {
                    return WaitForTerminalAsync(outbox, cancellationToken);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Could not encode terminal packet for session {SessionId}", sessionId);
                outbox.Close();
            }

            return Task.FromResult(false);
        }
    }

    private bool TrySendCore(long sessionId, IOutgoingPacket packet, INetworkConnection? expectedConnection)
    {
        lock (_gate)
        {
            if (!_running || !_connections.TryGet(sessionId, out var connection, out var disconnectRequested))
            {
                return false;
            }

            if (expectedConnection is not null && !ReferenceEquals(connection, expectedConnection))
            {
                return false;
            }

            if (!_outboxes.TryGetValue(sessionId, out var outbox))
            {
                outbox = new(
                    connection,
                    disconnectRequested,
                    _capacity,
                    id => _connections.DisconnectAsync(id, connection)
                );
                _outboxes.Add(sessionId, outbox);
                outbox.Start();
                _cleanups.Add(sessionId, ObserveCleanupAsync(sessionId, outbox.Completion, outbox));
            }
            else if (!ReferenceEquals(outbox.Connection, connection))
            {
                return false;
            }

            if (outbox.TerminalQueued)
            {
                return false;
            }

            try
            {
                if (outbox.TryWrite(PacketCodec.Encode(packet)))
                {
                    return true;
                }

                _logger.Warning("Outbound packet queue is full or closed for session {SessionId}", sessionId);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Could not encode outgoing packet for session {SessionId}", sessionId);
            }

            outbox.Close();

            return false;
        }
    }

    private async Task ObserveCleanupAsync(long sessionId, Task cleanup, SessionPacketOutbox? outbox)
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        try
        {
            await cleanup.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _failures.Add(exception);
            }

            _logger.Error(exception, "Outgoing connection cleanup failed for session {SessionId}", sessionId);
        }
        finally
        {
            lock (_gate)
            {
                if (outbox is not null &&
                    _outboxes.TryGetValue(sessionId, out var current) &&
                    ReferenceEquals(current, outbox))
                {
                    _outboxes.Remove(sessionId);
                }

                _cleanups.Remove(sessionId);
            }
        }
    }

    private static async Task<bool> WaitForTerminalAsync(SessionPacketOutbox outbox, CancellationToken cancellationToken)
    {
        await outbox.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);

        return outbox.TerminalSent;
    }

    private async Task StopCoreAsync()
    {
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        while (true)
        {
            Task[] pending;

            lock (_gate)
            {
                pending = _cleanups.Values.ToArray();

                if (pending.Length == 0)
                {
                    if (_failures.Count > 0)
                    {
                        throw new AggregateException(_failures);
                    }

                    return;
                }
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }
}
