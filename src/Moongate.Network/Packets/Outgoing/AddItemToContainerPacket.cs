using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using Moongate.UO.Data.Hues;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Add item to container (0x25): drops one item into an already-open container gump. 21 bytes — the
/// modern client carries a grid-location byte the older 20-byte form did not. Use container content
/// (0x3C) to send a whole container at once instead.
/// </summary>
public readonly record struct AddItemToContainerPacket(
    Serial Serial,
    ushort ItemId,
    ushort Amount,
    Point2D Position,
    Serial Container,
    Hue Hue
) : IOutgoingPacket
{
    public const byte PacketId = 0x25;

    public void Write(ref SpanWriter writer)
    {
        writer.Write(PacketId);
        writer.Write(Serial);
        writer.Write(ItemId);
        writer.Write((byte)0); // itemId offset, always zero for us
        writer.Write(Amount);
        writer.Write((short)Position.X);
        writer.Write((short)Position.Y);
        writer.Write((byte)0); // grid location, unused: the client packs the gump itself
        writer.Write(Container);
        writer.Write(Hue);
    }
}
