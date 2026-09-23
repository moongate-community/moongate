using Moongate.Network.Packets.General;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;

namespace Moongate.Tests.TestSupport.Packets;

public sealed class AsyncPingPacketHandler : IAsyncPacketHandler<PingPacket>
{
    public byte? LastSequence { get; private set; }

    public ValueTask HandleAsync(PacketContext context, PingPacket packet, CancellationToken cancellationToken)
    {
        LastSequence = packet.Sequence;
        return ValueTask.CompletedTask;
    }
}
