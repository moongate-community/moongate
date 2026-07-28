using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Tests.Support;

/// <summary>
/// Swallows every outgoing packet and reports no recipients. For tests that care about where entities
/// end up rather than what the client is told.
/// </summary>
public sealed class StubWorldService : IWorldService
{
    public int Broadcast<TPacket>(TPacket packet) where TPacket : IOutgoingPacket
        => 0;

    public void SendEnterWorld(PlayerSession session, MobileEntity mobile) { }

    public int SendToPlayer<TPacket>(Serial mobileId, TPacket packet) where TPacket : IOutgoingPacket
        => 0;

    public int SendToPlayersInRange<TPacket>(
        int mapId,
        Point3D center,
        int range,
        TPacket packet,
        Serial? exclude = null
    ) where TPacket : IOutgoingPacket
        => 0;
}
