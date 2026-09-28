using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Primitives;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Shows an item on the ground to a client from 7.0.0.0 (0xF3, 24 bytes), with two more bytes from 7.0.9.0 (High
///     Seas). Written as ModernUO does, without direction, light or flags; the packet has no length field.
/// </summary>
[PacketHandler(0xF3, PacketSizing.Variable, MinimumLength = 24)]
public sealed class WorldItemSaPacket : BasePacket<WorldItemSaPacket>, IOutgoingPacket
{
    private const int StygianAbyssLength = 24;
    private const int HighSeasLength = 26;

    public override int Length { get; }

    public Serial Serial { get; }

    public int ItemId { get; }

    public int Amount { get; }

    public Point3D Location { get; }

    public Hue Hue { get; }

    public bool HighSeas { get; }

    public WorldItemSaPacket(Serial serial, int itemId, int amount, Point3D location, Hue hue, bool highSeas)
    {
        Serial = serial;
        ItemId = itemId;
        Amount = amount;
        Location = location;
        Hue = hue;
        HighSeas = highSeas;
        Length = highSeas ? HighSeasLength : StygianAbyssLength;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian(0x0001);
        // 0 is an item; 1 a mobile, 2 a multi.
        writer.WriteByte(0);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)(ItemId & (HighSeas ? 0xFFFF : 0x7FFF)));
        writer.WriteByte(0);
        // The amount twice, as the smallest and the largest shown.
        writer.WriteUInt16BigEndian((ushort)Amount);
        writer.WriteUInt16BigEndian((ushort)Amount);
        writer.WriteUInt16BigEndian((ushort)(Location.X & 0x7FFF));
        writer.WriteUInt16BigEndian((ushort)(Location.Y & 0x3FFF));
        writer.WriteByte(unchecked((byte)(sbyte)Location.Z));
        writer.WriteByte(0);
        writer.WriteUInt16BigEndian(Hue.Value);
        writer.WriteByte(0);

        if (HighSeas)
        {
            writer.WriteUInt16BigEndian(0);
        }
    }
}
