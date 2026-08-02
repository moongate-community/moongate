using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Pick up item (0x07): the client asks to lift an item onto its cursor. <c>Amount</c> is how much of
/// a stack to take, and is clamped server-side to what the stack actually holds. 7 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers, Length = 7)]
public readonly record struct PickUpItemPacket(Serial Serial, ushort Amount) : IIncomingPacket<PickUpItemPacket>
{
    public static byte PacketId => 0x07;

    public static PickUpItemPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new(new(reader.ReadUInt32()), reader.ReadUInt16());
    }
}
