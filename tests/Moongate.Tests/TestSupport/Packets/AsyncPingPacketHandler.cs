using Moongate.Network.Packets.General;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class AsyncPingPacketHandler : IAsyncPacketHandler<PingPacket>
{
    public Func<PacketContext, PingPacket, CancellationToken, ValueTask>? OnHandleAsync { get; set; }

    public byte? LastSequence { get; private set; }

    public ValueTask HandleAsync(PacketContext context, PingPacket packet, CancellationToken cancellationToken)
    {
        LastSequence = packet.Sequence;

        return OnHandleAsync?.Invoke(context, packet, cancellationToken) ?? ValueTask.CompletedTask;
    }
}
