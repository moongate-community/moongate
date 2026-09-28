using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Sets the season the client draws the map in, optionally with the season's sound (0xBC).
/// </summary>
[PacketHandler(0xBC, PacketSizing.Fixed, Length = 3)]
public sealed class SeasonChangePacket : BaseFixedPacket<SeasonChangePacket>, IOutgoingPacket
{
    public SeasonType Season { get; }

    public bool PlaySound { get; }

    public SeasonChangePacket(SeasonType season, bool playSound)
    {
        Season = season;
        PlaySound = playSound;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Season);
        writer.WriteByte(PlaySound ? (byte)1 : (byte)0);
    }
}
