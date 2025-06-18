using System.Net;
using Moongate.Core.Extensions.Network;
using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets;

public class ConnectToGameServerPacket : BaseUoPacket
{
    public IPAddress ServerAddress { get; set; }
    public int ServerPort { get; set; }
    public int AuthKey { get; set; }

    public ConnectToGameServerPacket() : base(0x8C)
    {
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.WriteLE(ServerAddress.ToRawAddress());
        writer.Write((short)ServerPort);
        writer.Write(AuthKey);

        return writer.ToArray();
    }
}
