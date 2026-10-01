using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Plays a music track on the client (0x6D), or stops the music with <see cref="MusicType.NoMusic" />.
/// </summary>
[PacketHandler(0x6D, PacketSizing.Fixed, Length = 3)]
public sealed class PlayMusicPacket : BaseFixedPacket<PlayMusicPacket>, IOutgoingPacket
{
    public MusicType Music { get; }

    public PlayMusicPacket(MusicType music)
    {
        Music = music;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Music);
    }
}
