using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Tests.Support;

/// <summary>
/// Records what would have gone out instead of sending it, so a test can assert which packet reached
/// whom. The counterpart of <see cref="StubWorldService" />, which swallows everything silently.
/// </summary>
public sealed class RecordingWorldService : IWorldService
{
    public List<(int MapId, Point3D Center, int Range, IOutgoingPacket Packet)> InRange { get; } = [];

    public List<(Serial MobileId, IOutgoingPacket Packet)> ToPlayer { get; } = [];

    public List<IOutgoingPacket> Broadcasts { get; } = [];

    public int Broadcast<TPacket>(TPacket packet) where TPacket : IOutgoingPacket
    {
        Broadcasts.Add(packet);

        return 1;
    }

    public void SendEnterWorld(PlayerSession session, MobileEntity mobile) { }

    public int SendToPlayer<TPacket>(Serial mobileId, TPacket packet) where TPacket : IOutgoingPacket
    {
        ToPlayer.Add((mobileId, packet));

        return 1;
    }

    public int SendToPlayersInRange<TPacket>(
        int mapId,
        Point3D center,
        int range,
        TPacket packet,
        Serial? exclude = null
    ) where TPacket : IOutgoingPacket
    {
        InRange.Add((mapId, center, range, packet));

        return 1;
    }
}
