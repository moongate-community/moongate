using System.Net;
using Moongate.Core.Spans;

namespace Moongate.UO.Data.Packets.Data;

public class GameServerEntry
{
    public int Index { get; set; }
    public string ServerName { get; set; }
    public IPAddress IpAddress { get; set; }

    public ReadOnlyMemory<byte> Write()
    {
        using var writer = new SpanWriter(1, true);

        writer.Write((short)Index);
        writer.WriteAscii(ServerName, 32);
        writer.Write((byte)0);
        writer.Write((byte)0);

        var ipBytes = IpAddress.GetAddressBytes();
        Array.Reverse(ipBytes);
        uint ipReversed = BitConverter.ToUInt32(ipBytes, 0);
        writer.Write(ipReversed);

        return writer.ToArray().AsMemory();
    }


}
