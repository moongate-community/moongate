using Moongate.Core.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Tells the client to walk its character to a spot by itself (0x38), as it does after a double right click on
///     the ground: the client finds the way and sends its steps one by one.
/// </summary>
/// <remarks>
///     ModernUO's <c>SendPathfindMessage</c>: x, y and z, two bytes each.
/// </remarks>
[PacketHandler(0x38, PacketSizing.Fixed, Length = 7, Description = "Pathfind")]
public sealed class PathfindPacket : BaseFixedPacket<PathfindPacket>, IOutgoingPacket
{
    public Point3D Destination { get; }

    public PathfindPacket(Point3D destination)
    {
        Destination = destination;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Destination.X);
        writer.WriteUInt16BigEndian((ushort)Destination.Y);
        writer.WriteUInt16BigEndian((ushort)(short)Destination.Z);
    }
}
