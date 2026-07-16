using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Select server (0xA0): the shard index the client picked from the server list.</summary>
[PacketDocumentation(PacketFamilyType.LoginShardSelect, Length = 3)]
public readonly record struct SelectServerPacket(ushort ShardIndex) : IIncomingPacket<SelectServerPacket>
{
    public static byte PacketId => 0xA0;

    public static SelectServerPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new(reader.ReadUInt16());
    }
}
