using System.Collections.Concurrent;
using System.Threading;
using Moongate.Network.Client;
using Moongate.Server.Data.Packets;
using Moongate.Server.Interfaces.Services.Packets;

namespace Moongate.Tests.Server.Support;

public sealed class GameLoopBlockingOutboundPacketSender : IOutboundPacketSender, IDisposable
{
    private readonly ManualResetEventSlim _firstSendStarted = new(false);
    private readonly ManualResetEventSlim _sendGate = new(false);
    private int _sendCalls;

    public ConcurrentQueue<OutgoingGamePacket> SentPackets { get; } = new();

    public bool WaitForFirstSendStart(TimeSpan timeout)
        => _firstSendStarted.Wait(timeout);

    public void ReleaseBlockedSend()
        => _sendGate.Set();

    public int SendCalls => Volatile.Read(ref _sendCalls);

    public bool Send(MoongateTCPClient client, OutgoingGamePacket outgoingPacket)
    {
        var currentCall = Interlocked.Increment(ref _sendCalls);
        if (currentCall == 1)
        {
            _firstSendStarted.Set();
            _sendGate.Wait();
        }

        SentPackets.Enqueue(outgoingPacket);
        return true;
    }

    public Task<bool> SendAsync(
        MoongateTCPClient client,
        OutgoingGamePacket outgoingPacket,
        CancellationToken cancellationToken
    )
        => Task.FromResult(Send(client, outgoingPacket));

    public void Dispose()
    {
        _firstSendStarted.Dispose();
        _sendGate.Dispose();
    }
}
