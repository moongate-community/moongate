using System.Collections.ObjectModel;

using Moongate.Network.Packets.Data.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Login;

public sealed class ServerListPacket : IOutgoingPacket
{
    private const byte PacketOpCode = 0xA8;
    private const byte SystemInfoFlag = 0x5D;
    private const int HeaderLength = 6;
    private const int EntryLength = 40;
    private const int NameLength = 32;
    private const int MaximumServerCount = (ushort.MaxValue - HeaderLength) / EntryLength;

    public byte OpCode => PacketOpCode;
    public int Length { get; }
    public IReadOnlyList<GameServerEntry> Servers { get; }

    public ServerListPacket(IEnumerable<GameServerEntry> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);
        var snapshot = servers.ToArray();
        if (snapshot.Any(server => server is null))
        {
            throw new ArgumentException("The server list must not contain null entries.", nameof(servers));
        }

        if (snapshot.Length > MaximumServerCount)
        {
            throw new ArgumentException($"The server list cannot contain more than {MaximumServerCount} entries.", nameof(servers));
        }

        Length = HeaderLength + EntryLength * snapshot.Length;
        Servers = new ReadOnlyCollection<GameServerEntry>(snapshot);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteByte(SystemInfoFlag);
        writer.WriteUInt16BigEndian((ushort)Servers.Count);

        foreach (var server in Servers)
        {
            writer.WriteUInt16BigEndian(server.ServerIndex);
            writer.WriteFixedAscii(server.Name, NameLength);
            writer.WriteByte(server.FullPercent);
            writer.WriteByte(unchecked((byte)server.TimeZone));
            var address = server.GetAddressBytes();
            writer.WriteByte(address[3]);
            writer.WriteByte(address[2]);
            writer.WriteByte(address[1]);
            writer.WriteByte(address[0]);
        }
    }
}
