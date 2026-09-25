using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Packets;

/// <summary>
///     Handles a login packet asynchronously without a game loop.
/// </summary>
public interface ILoginPacketHandler<TPacket> where TPacket : class, IIncomingPacket<TPacket>
{
    ValueTask HandleAsync(LoginSession session, TPacket packet, CancellationToken cancellationToken);
}
