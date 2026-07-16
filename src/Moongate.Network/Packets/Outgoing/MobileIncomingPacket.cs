using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Attributes;
using Moongate.Network.Data;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Mobile incoming / draw object (0x78): draws a mobile and its equipped items on the client. Uses
/// the modern layout (full 16-bit item ids, hue always written). Variable length.
/// </summary>
[PacketDocumentation(PacketFamilyType.Movement)]
public readonly record struct MobileIncomingPacket(
    Serial Serial,
    ushort Body,
    ushort X,
    ushort Y,
    sbyte Z,
    DirectionType Direction,
    Hue Hue,
    byte Flags,
    NotorietyType Notoriety,
    IReadOnlyList<MobileIncomingItem> Items
) : IOutgoingPacket
{
    public const byte PacketId = 0x78;

    private const int HeaderLength = 19; // id + length + serial + body + x + y + z + dir + hue + flags + notoriety
    private const int ItemLength = 9;    // serial + itemId + layer + hue
    private const int TerminatorLength = 4;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write((ushort)(HeaderLength + ItemLength * Items.Count + TerminatorLength));
        writer.Write(Serial);
        writer.Write(Body);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Z);
        writer.Write((byte)Direction);
        writer.Write(Hue);
        writer.Write(Flags);
        writer.Write((byte)Notoriety);

        foreach (var item in Items)
        {
            writer.Write(item.Serial);
            writer.Write(item.ItemId);
            writer.Write((byte)item.Layer);
            writer.Write(item.Hue);
        }

        writer.Write(0); // terminating serial 0
    }
}
