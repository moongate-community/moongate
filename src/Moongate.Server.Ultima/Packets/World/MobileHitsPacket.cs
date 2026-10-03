using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     The hit points of a mobile (0xA1, 9 bytes): the maximum, then the current value. Normalized, as the others see
///     them, they are a share of 100 instead of the real numbers.
/// </summary>
[PacketHandler(0xA1, PacketSizing.Fixed, Length = 9)]
public sealed class MobileHitsPacket : BaseFixedPacket<MobileHitsPacket>, IOutgoingPacket
{
    private const int Share = 100;

    public Serial Serial { get; }

    public int Hits { get; }

    public int HitsMax { get; }

    public MobileHitsPacket(Serial serial, int hits, int hitsMax, bool normalized = false)
    {
        Serial = serial;

        // As ModernUO: a mobile without a maximum is sent as it is.
        if (normalized && hitsMax != 0)
        {
            Hits = hits * Share / hitsMax;
            HitsMax = Share;
        }
        else
        {
            Hits = hits;
            HitsMax = hitsMax;
        }
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)HitsMax);
        writer.WriteUInt16BigEndian((ushort)Hits);
    }
}
