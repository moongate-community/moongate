using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;

namespace Moongate.Network.Packets.Outgoing.World;

[PacketHandler(0x76, PacketSizing.Fixed, Length = 16, Description = "Server Change")]
public sealed class ServerChangePacket : BaseGameNetworkPacket
{
    public Point3D Location { get; set; }

    public byte Unknown0 { get; set; }

    public short Unknown1 { get; set; }

    public short Unknown2 { get; set; }

    public ushort MapWidth { get; set; }

    public ushort MapHeight { get; set; }

    public ServerChangePacket()
        : base(0x76, 16) { }

    public ServerChangePacket(
        Point3D location,
        ushort mapWidth,
        ushort mapHeight,
        byte unknown0 = 0,
        short unknown1 = 0,
        short unknown2 = 0
    ) : this()
    {
        Location = location;
        MapWidth = mapWidth;
        MapHeight = mapHeight;
        Unknown0 = unknown0;
        Unknown1 = unknown1;
        Unknown2 = unknown2;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((short)Location.X);
        writer.Write((short)Location.Y);
        writer.Write((short)Location.Z);
        writer.Write(Unknown0);
        writer.Write(Unknown1);
        writer.Write(Unknown2);
        writer.Write(MapWidth);
        writer.Write(MapHeight);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 15)
        {
            return false;
        }

        var x = reader.ReadInt16();
        var y = reader.ReadInt16();
        var z = reader.ReadInt16();
        Unknown0 = reader.ReadByte();
        Unknown1 = reader.ReadInt16();
        Unknown2 = reader.ReadInt16();
        MapWidth = reader.ReadUInt16();
        MapHeight = reader.ReadUInt16();
        Location = new Point3D(x, y, z);

        return reader.Remaining == 0;
    }
}
