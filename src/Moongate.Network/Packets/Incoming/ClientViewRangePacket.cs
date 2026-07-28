using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Client view range (0xC8): the client announces the update range it wants, in tiles, whenever the
/// option changes. The server clamps it and echoes the accepted value back with the same opcode.
/// 2 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.WorldState, Length = 2)]
public readonly record struct ClientViewRangePacket(byte Range) : IIncomingPacket<ClientViewRangePacket>
{
    public static byte PacketId => 0xC8;

    public static ClientViewRangePacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new(reader.ReadByte());
    }
}
