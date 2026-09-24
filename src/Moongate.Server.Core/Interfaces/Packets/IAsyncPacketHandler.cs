using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Core.Interfaces.Packets;

/// <summary>Handles a packet off the game loop when asynchronous I/O is required.</summary>
public interface IAsyncPacketHandler<TPacket> where TPacket : class, IIncomingPacket<TPacket>
{
    ValueTask HandleAsync(PacketContext context, TPacket packet, CancellationToken cancellationToken);
}
