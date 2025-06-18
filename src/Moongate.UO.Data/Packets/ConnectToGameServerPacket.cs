using System.Net;
using Moongate.Core.Extensions.Network;
using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets;

public class ConnectToGameServerPacket : BaseUoPacket
{
    public IPAddress ServerAddress { get; set; }
    public int ServerPort { get; set; }
    public uint AuthKey { get; set; }

    public ConnectToGameServerPacket() : base(0x8C)
    {
    }
    public ConnectToGameServerPacket(IPAddress serverAddress, int serverPort, uint authKey) : this()
    {
        ServerAddress = serverAddress;
        ServerPort = serverPort;
        AuthKey = authKey;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.WriteLE(ServerAddress.ToRawAddress());
        writer.Write((short)ServerPort);
        writer.Write(AuthKey);

        return writer.ToArray();
    }
}
