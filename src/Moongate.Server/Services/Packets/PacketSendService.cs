using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Packets.Internal;
using Serilog;

namespace Moongate.Server.Services.Packets;

/// <summary>Snapshots outgoing packets and sends them through bounded per-connection queues.</summary>
public sealed class PacketSendService : IPacketSendService
{
    private readonly Lock _gate = new();
    private readonly ISessionService _sessions;
    private readonly int _capacity;
    private readonly Dictionary<long, SessionPacketOutbox> _outboxes = new();
    private readonly ILogger _logger = Log.ForContext<PacketSendService>();

    private bool _running;
    private bool _stopped;

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

    public PacketSendService(ISessionService sessions, int capacity = 128)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _sessions = sessions;
        _capacity = capacity;
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

            return Task.WhenAll(_outboxes.Values.Select(outbox => outbox.Completion));
        }
    }

    /// <inheritdoc />
    public bool TrySend(long sessionId, IOutgoingPacket packet)
    {
        lock (_gate)
        {
            if (!_running || !_sessions.TryGet(sessionId, out var session) ||
                session.NetworkSession.Client is not { IsConnected: true } client)
            {
                return false;
            }

            if (!_outboxes.TryGetValue(sessionId, out var outbox))
            {
                outbox = new SessionPacketOutbox(client, _capacity, Retire);
                _outboxes.Add(sessionId, outbox);
                outbox.Start();
            }

            try
            {
                // Snapshot before returning: callers may reuse their outgoing packet after admission.
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

            if (_sessions.TryGet(sessionId, out var session) && session.NetworkSession.Client is { } client)
            {
                // Closing changes IsConnected synchronously, preventing subsequent admission even
                // while the dispatcher still owns retirement of this session from its registry.
                client.Dispose();
                return client.Completion;
            }

            return Task.CompletedTask;
        }
    }

    private void Retire(long sessionId)
    {
        lock (_gate)
        {
            _outboxes.Remove(sessionId);
        }
    }
}
