using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Packets.Data;

namespace Moongate.UO.Data.Packets.Login;

/// <summary>
/// OpCode 0x5E - Server Listing
/// Sends list of available game servers to client during login
/// </summary>
public class ServerListingPacket : BaseUoPacket
{
    public List<ServerEntry> Servers { get; set; }

    public ServerListingPacket() : base(0x5E)
        => Servers = new();

    public ServerListingPacket(params ServerEntry[] entries) : this()
        => Servers = new(entries);

    public void AddServer(ServerEntry entry)
    {
        Servers.Add(entry);
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write((ushort)0); // Placeholder for length
        writer.Write((ushort)Servers.Count);

        foreach (var server in Servers)
        {
            writer.Write(server.Index);
            writer.WriteAscii(server.ServerName, 32);
        }

        writer.WritePacketLength();

        return writer.ToArray();
    }
}
