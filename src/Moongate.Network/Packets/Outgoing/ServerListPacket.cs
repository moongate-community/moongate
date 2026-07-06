using System.Net;
using System.Text;
using Moongate.Network.Helpers;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>Server list (0xA8): advertises the available shards. Card 09 sends the single shard.</summary>
public readonly record struct ServerListPacket(string ShardName, IPAddress Address)
{
    public const byte PacketId = 0xA8;

    private const int NameLength = 32;
    private const byte SystemInfoFlag = 0x5D;

    public void Write(ref SpanWriter writer)
    {
        // header(6) + one entry(40) = 46
        const ushort total = 6 + 2 + NameLength + 1 + 1 + 4;

        writer.Write(PacketId);
        writer.Write(total);
        writer.Write(SystemInfoFlag);
        writer.Write((ushort)1); // server count

        writer.Write((ushort)0); // server index

        Span<byte> name = stackalloc byte[NameLength];
        var written = Encoding.ASCII.GetBytes(ShardName, name);
        name[written..].Clear();

        foreach (var b in name)
        {
            writer.Write(b);
        }

        writer.Write((byte)0); // percent full
        writer.Write((byte)0); // timezone
        IpV4Writer.WriteReversed(ref writer, Address);
    }
}
