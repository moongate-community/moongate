using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Data;

namespace Moongate.UO.Data.Packets.Login;

public class ShardListPacket : BaseUoPacket
{
    public List<GameServerEntry> Shards { get; set; }

    public ShardListPacket(params GameServerEntry[] entries) : base(0xA8)
    {
        Shards = new List<GameServerEntry>(entries);
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);

        var length = 6 + 40 * Shards.Count;
        writer.Write((ushort)length);
        writer.Write((byte)0x5D);
        writer.Write((ushort)Shards.Count);
        foreach (var shared in Shards)
        {
            writer.Write(shared.Write().Span);
        }

        return writer.ToArray();
    }

    public void AddShard(GameServerEntry entry)
    {
        Shards.Add(entry);
    }
}
