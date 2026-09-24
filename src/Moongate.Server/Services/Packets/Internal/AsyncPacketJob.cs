using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Services.Packets.Internal;

internal sealed class AsyncPacketJob
{
    public GameSession Session { get; }
    public IPacket Packet { get; }
    public Func<PacketContext, IPacket, CancellationToken, ValueTask> Handler { get; }
    public CancellationTokenSource Cancellation { get; }
    public bool Enqueued { get; set; }

    public AsyncPacketJob(
        GameSession session,
        IPacket packet,
        Func<PacketContext, IPacket, CancellationToken, ValueTask> handler,
        CancellationToken stoppingToken
    )
    {
        Session = session;
        Packet = packet;
        Handler = handler;
        Cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
    }
}
