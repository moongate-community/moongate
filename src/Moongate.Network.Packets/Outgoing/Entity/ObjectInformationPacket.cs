using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Network.Packets.Outgoing.Entity;

[PacketHandler(0xF3, PacketSizing.Fixed, Length = 24, Description = "Object Information (SA)")]

/// <summary>
/// Represents Object Information (SA) packet (0xF3).
/// </summary>
public class ObjectInformationPacket : BaseGameNetworkPacket
{
    private const ushort Command = 0x0001;

    public byte DataType { get; set; }

    public Serial Serial { get; set; }

    public ushort Graphic { get; set; }

    public byte Facing { get; set; }

    public ushort AmountFirst { get; set; } = 1;

    public ushort AmountSecond { get; set; } = 1;

    public ushort X { get; set; }

    public ushort Y { get; set; }

    public sbyte Z { get; set; }

    public byte Layer { get; set; }

    public ushort Color { get; set; }

    public ObjectInfoFlags Flags { get; set; }

    public ObjectInformationPacket()
        : base(0xF3, 24) { }

    public ObjectInformationPacket(
        UOItemEntity item,
        byte facing = 0,
        byte layer = 0,
        ObjectInfoFlags flags = ObjectInfoFlags.None
    )
        : this()
    {
        ArgumentNullException.ThrowIfNull(item);

        DataType = 0x00;
        Serial = item.Id;
        Graphic = unchecked((ushort)item.ItemId);
        Facing = facing;

        var amount = (ushort)Math.Clamp(item.Amount, 1, ushort.MaxValue);
        AmountFirst = amount;
        AmountSecond = amount;

        X = unchecked((ushort)item.Location.X);
        Y = unchecked((ushort)item.Location.Y);
        Z = unchecked((sbyte)item.Location.Z);
        Layer = layer;
        Color = unchecked((ushort)item.Hue);
        Flags = flags;
    }

    public static ObjectInformationPacket ForMulti(Serial serial, ushort graphic, Point3D location)
        => new()
        {
            DataType = 0x02,
            Serial = serial,
            Graphic = graphic,
            Facing = 0x00,
            AmountFirst = 0x0001,
            AmountSecond = 0x0001,
            X = unchecked((ushort)location.X),
            Y = unchecked((ushort)location.Y),
            Z = unchecked((sbyte)location.Z),
            Layer = 0x00,
            Color = 0x0000,
            Flags = ObjectInfoFlags.None
        };

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Command);
        writer.Write(DataType);
        writer.Write((uint)Serial);
        writer.Write(Graphic);
        writer.Write(Facing);
        writer.Write(AmountFirst);
        writer.Write(AmountSecond);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Z);
        writer.Write(Layer);
        writer.Write(Color);
        writer.Write((byte)Flags);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 23)
        {
            return false;
        }

        var command = reader.ReadUInt16();

        if (command != Command)
        {
            return false;
        }

        DataType = reader.ReadByte();
        Serial = (Serial)reader.ReadUInt32();
        Graphic = reader.ReadUInt16();
        Facing = reader.ReadByte();
        AmountFirst = reader.ReadUInt16();
        AmountSecond = reader.ReadUInt16();
        X = reader.ReadUInt16();
        Y = reader.ReadUInt16();
        Z = reader.ReadSByte();
        Layer = reader.ReadByte();
        Color = reader.ReadUInt16();
        Flags = (ObjectInfoFlags)reader.ReadByte();

        return reader.Remaining == 0;
    }
}
