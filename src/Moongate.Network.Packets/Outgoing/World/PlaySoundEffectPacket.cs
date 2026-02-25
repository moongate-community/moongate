using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
using Moongate.UO.Data.Geometry;

namespace Moongate.Network.Packets.Outgoing.World;

[PacketHandler(0x54, PacketSizing.Fixed, Length = 12, Description = "Play Sound Effect")]
/// <summary>
/// Represents Play Sound Effect packet (0x54).
/// </summary>
public class PlaySoundEffectPacket : BaseGameNetworkPacket
{
    public byte Mode { get; set; } = 0x01;

    public ushort SoundModel { get; set; }

    public ushort Unknown3 { get; set; }

    public Point3D Location { get; set; }

    public PlaySoundEffectPacket()
        : base(0x54, 12) { }

    public PlaySoundEffectPacket(byte mode, ushort soundModel, ushort unknown3, Point3D location)
        : this()
    {
        Mode = mode;
        SoundModel = soundModel;
        Unknown3 = unknown3;
        Location = location;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(Mode);
        writer.Write(SoundModel);
        writer.Write(Unknown3);
        writer.Write((ushort)Location.X);
        writer.Write((ushort)Location.Y);
        writer.Write((short)Location.Z);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining != 11)
        {
            return false;
        }

        Mode = reader.ReadByte();
        SoundModel = reader.ReadUInt16();
        Unknown3 = reader.ReadUInt16();
        var x = reader.ReadUInt16();
        var y = reader.ReadUInt16();
        var z = reader.ReadInt16();
        Location = new(x, y, z);

        return reader.Remaining == 0;
    }
}
