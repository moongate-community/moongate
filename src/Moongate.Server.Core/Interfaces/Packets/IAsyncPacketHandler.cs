using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Packets;

namespace Moongate.Server.Core.Interfaces.Packets;

/// <summary>Handles a packet off the game loop when asynchronous I/O is required.</summary>
public interface IAsyncPacketHandler<TPacket> where TPacket : class, IIncomingPacket<TPacket>
{
    /// <summary>Handles a decoded game packet asynchronously for its packet context.</summary>
    /// <param name="context">The connection and dispatch context.</param>
    /// <param name="packet">The decoded packet.</param>
    /// <param name="cancellationToken">Cancels handling when dispatch stops.</param>
    /// <returns>A task-like value that completes when handling finishes.</returns>
    ValueTask HandleAsync(PacketContext context, TPacket packet, CancellationToken cancellationToken);
}
