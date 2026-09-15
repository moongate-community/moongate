using System.Collections.ObjectModel;

using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Data.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Internal.Login;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Outgoing.Login;

[PacketHandler(0xA8, PacketSizing.Variable, MinimumLength = 6)]
public sealed class ServerListPacket : BasePacket<ServerListPacket>, IOutgoingPacket
{
    private const byte SystemInfoFlag = 0x5D;
    private static readonly int MaximumServerCount =
        (ushort.MaxValue - Descriptor.MinimumLength) / LoginProtocolConstants.ServerEntryLength;

    public override int Length { get; }
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

        Length = Descriptor.MinimumLength + LoginProtocolConstants.ServerEntryLength * snapshot.Length;
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
            writer.WriteFixedAscii(server.Name, LoginProtocolConstants.ServerNameLength);
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
