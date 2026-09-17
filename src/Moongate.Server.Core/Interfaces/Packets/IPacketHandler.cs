using Moongate.Network.Packets.Interfaces;
using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Core.Interfaces.Packets;

/// <summary>Handles one incoming packet type synchronously on the game loop.</summary>
public interface IPacketHandler<TPacket> where TPacket : class, IIncomingPacket<TPacket>
{
    /// <summary>Processes the packet for its live session on the game loop thread.</summary>
    void Handle(GameSession session, TPacket packet);
}
