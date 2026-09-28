using System.Collections.ObjectModel;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Shows a mobile with everything it wears (0x78, variable). Uses the layout of clients 7.0.33 and later: every item
///     carries its hue and the item id has no hue flag.
/// </summary>
[PacketHandler(0x78, PacketSizing.Variable, MinimumLength = 23)]
public sealed class MobileIncomingPacket : BasePacket<MobileIncomingPacket>, IOutgoingPacket
{
    private const int FixedLength = 23;
    private const int ItemLength = 9;

    public override int Length { get; }

    public Serial Serial { get; }

    public int Body { get; }

    public Point3D Location { get; }

    public DirectionType Direction { get; }

    public Hue Hue { get; }

    public MobileFlagsType Flags { get; }

    public NotorietyType Notoriety { get; }

    /// <summary>
    ///     Gets the worn items, one per layer, hair and beard included.
    /// </summary>
    public IReadOnlyList<MobileEquipmentEntry> Equipment { get; }

    public MobileIncomingPacket(
        Serial serial,
        int body,
        Point3D location,
        DirectionType direction,
        Hue hue,
        MobileFlagsType flags,
        NotorietyType notoriety,
        IEnumerable<MobileEquipmentEntry> equipment
    )
    {
        ArgumentNullException.ThrowIfNull(equipment);
        var items = equipment.ToArray();

        // The client keeps one item per layer; a second would replace the first on screen.
        if (items.Select(item => item.Layer).Distinct().Count() != items.Length)
        {
            throw new ArgumentException("A mobile can wear only one item per layer.", nameof(equipment));
        }

        Serial = serial;
        Body = body;
        Location = location;
        Direction = direction;
        Hue = hue;
        Flags = flags;
        Notoriety = notoriety;
        Equipment = new ReadOnlyCollection<MobileEquipmentEntry>(items);
        Length = FixedLength + ItemLength * items.Length;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)Body);
        writer.WriteUInt16BigEndian((ushort)Location.X);
        writer.WriteUInt16BigEndian((ushort)Location.Y);
        writer.WriteByte(unchecked((byte)(sbyte)Location.Z));
        writer.WriteByte((byte)Direction);
        writer.WriteUInt16BigEndian(Hue.Value);
        writer.WriteByte((byte)Flags);
        writer.WriteByte((byte)Notoriety);

        foreach (var item in Equipment)
        {
            writer.WriteSerial(item.Serial);
            writer.WriteUInt16BigEndian((ushort)item.ItemId);
            writer.WriteByte((byte)item.Layer);
            writer.WriteUInt16BigEndian(item.Hue.Value);
        }

        // A zero serial ends the item list.
        writer.WriteUInt32BigEndian(0);
    }
}
