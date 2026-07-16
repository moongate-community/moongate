using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.UO.Data.Hues;
using Moongate.Ultima.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Worn item (0x2E): draws a single item on a mobile that the client already knows about. 15 bytes
/// fixed. Equipment sent at the moment a mobile first appears rides inside mobile incoming (0x78)
/// instead; this is for equipping and re-hueing afterwards.
/// </summary>
public readonly record struct WornItemPacket(Serial Serial, ushort ItemId, LayerType Layer, Serial Mobile, Hue Hue)
    : IOutgoingPacket
{
    public const byte PacketId = 0x2E;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(ItemId);
        writer.Write((byte)0); // itemId offset, always zero for us
        writer.Write((byte)Layer);
        writer.Write(Mobile);
        writer.Write(Hue);
    }
}
