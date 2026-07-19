using System.Buffers.Binary;
using System.Text;
using Moongate.Network.Interfaces;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class CharacterDeletePacketsTests
{
    [Fact]
    public void CharacterDeleteResult_IsTwoBytesWithTheReason()
    {
        var bytes = Serialize(new CharacterDeleteResultPacket(DeleteResultType.CharBeingPlayed));

        Assert.Equal(new byte[] { 0x85, 0x02 }, bytes);
    }

    [Fact]
    public void CharacterListUpdate_WritesEverySlotAndPadsTheEmptyOnes()
    {
        var bytes = Serialize(new CharacterListUpdatePacket(["Hero", "Freydis"], 7));

        Assert.Equal(0x86, bytes[0]);
        Assert.Equal(4 + 60 * 7, bytes.Length);
        Assert.Equal(4 + 60 * 7, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(1)));
        Assert.Equal(7, bytes[3]);

        Assert.Equal("Hero", Encoding.ASCII.GetString(bytes, 4, 30).TrimEnd('\0'));
        Assert.Equal("Freydis", Encoding.ASCII.GetString(bytes, 64, 30).TrimEnd('\0'));

        // The five unused slots are blank, not leftovers.
        Assert.Equal(string.Empty, Encoding.ASCII.GetString(bytes, 124, 30).TrimEnd('\0'));
    }

    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(1024, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }
}
