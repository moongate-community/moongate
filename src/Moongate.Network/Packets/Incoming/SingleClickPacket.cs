using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Single click (0x09): the client clicked an entity, identified by its serial. 5 bytes fixed.
/// Whether the target is a mobile or an item is decided later from the serial's range.
/// </summary>
[PacketDocumentation(PacketFamilyType.InteractionKeepalive, Length = 5)]
public readonly record struct SingleClickPacket(Serial Target) : IIncomingPacket<SingleClickPacket>
{
    public static byte PacketId => 0x09;

    public static SingleClickPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id
        var target = new Serial(reader.ReadUInt32());

        return new(target);
    }
}
