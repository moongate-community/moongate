using Moongate.Network.Interfaces;
using Moongate.Server.Abstractions.Data;

namespace Moongate.Server.Abstractions.Interfaces.Network;

/// <summary>
/// Handles a decoded packet of type <typeparamref name="TPacket" /> for a session. Runs on the
/// network thread: decode and respond, or post world mutations to the game loop (from card 11 on).
/// </summary>
/// <typeparam name="TPacket">The incoming packet this handler consumes.</typeparam>
public interface IPacketHandler<in TPacket> where TPacket : IIncomingPacket<TPacket>
{
    void Handle(TPacket packet, in PacketContext context);
}
