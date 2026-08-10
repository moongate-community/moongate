using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Drop-wear item (0x13): the client dropped what it was holding onto a paperdoll. <c>Mobile</c> is who
/// to put it on — normally the player themselves. 10 bytes fixed.
/// <para>
/// <c>Layer</c> is where the client believes the item goes, and the server does not take its word for
/// it: which layer an item occupies is a property of the item. POL validates this byte and then assigns
/// the item's own tile layer over it (eqpitem.cpp), for the same reason — honouring the request would
/// let a client wear a dagger as a pair of boots.
/// </para>
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers, Length = 10)]
public readonly record struct DropWearItemPacket(Serial Serial, byte Layer, Serial Mobile)
    : IIncomingPacket<DropWearItemPacket>
{
    public static byte PacketId => 0x13;

    public static DropWearItemPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new(new(reader.ReadUInt32()), reader.ReadByte(), new(reader.ReadUInt32()));
    }
}
