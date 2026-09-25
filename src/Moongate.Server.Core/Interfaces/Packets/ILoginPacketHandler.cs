using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Packets;

/// <summary>
///     Handles a login packet asynchronously without a game loop.
/// </summary>
public interface ILoginPacketHandler<TPacket> where TPacket : class, IIncomingPacket<TPacket>
{
    /// <summary>
    ///     Handles a decoded login packet for the current login session.
    /// </summary>
    /// <param name="session">
    ///     The connection's login session.
    /// </param>
    /// <param name="packet">
    ///     The decoded packet.
    /// </param>
    /// <param name="cancellationToken">
    ///     Cancels handling when the connection closes or the dispatcher stops.
    /// </param>
    /// <returns>
    ///     A task-like value that completes when handling finishes.
    /// </returns>
    ValueTask HandleAsync(LoginSession session, TPacket packet, CancellationToken cancellationToken);
}
