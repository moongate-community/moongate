using Moongate.Core.Geometry;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Plays a sound once at a location (0x54), as ModernUO's sound effect: flags 1, the sound, volume 0, then x, y, z.
/// </summary>
[PacketHandler(0x54, PacketSizing.Fixed, Length = 12)]
public sealed class PlaySoundPacket : BaseFixedPacket<PlaySoundPacket>, IOutgoingPacket
{
    public int Sound { get; }

    public Point3D Location { get; }

    public PlaySoundPacket(int sound, Point3D location)
    {
        Sound = sound;
        Location = location;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte(1);
        writer.WriteUInt16BigEndian((ushort)Sound);
        writer.WriteUInt16BigEndian(0);
        writer.WriteUInt16BigEndian((ushort)Location.X);
        writer.WriteUInt16BigEndian((ushort)Location.Y);
        writer.WriteUInt16BigEndian((ushort)(short)Location.Z);
    }
}
