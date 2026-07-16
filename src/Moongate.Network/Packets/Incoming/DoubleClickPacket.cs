using Moongate.Core.Primitives;
using Moongate.Network.Interfaces;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Double click (0x06): the client double-clicked an entity, identified by its serial. 5 bytes fixed.
/// Whether the target is a mobile or an item is decided later from the serial's range.
/// </summary>
public readonly record struct DoubleClickPacket(Serial Target) : IIncomingPacket<DoubleClickPacket>
{
    public static byte PacketId => 0x06;

    public static DoubleClickPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id
        var target = new Serial(reader.ReadUInt32());

        return new(target);
    }
}
