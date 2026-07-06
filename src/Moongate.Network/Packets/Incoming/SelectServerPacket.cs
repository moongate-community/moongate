using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>Select server (0xA0): the shard index the client picked from the server list.</summary>
public readonly record struct SelectServerPacket(ushort ShardIndex)
{
    public const byte PacketId = 0xA0;

    public static SelectServerPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new SelectServerPacket(reader.ReadUInt16());
    }
}
