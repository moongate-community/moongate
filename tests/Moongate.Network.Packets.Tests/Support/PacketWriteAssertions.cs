using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;

namespace Moongate.Network.Packets.Tests.Support;

internal static class PacketWriteAssertions
{
    public static void AssertAtomicFailure(IOutgoingPacket packet)
    {
        var destination = Enumerable.Repeat((byte)0xCC, packet.Length - 1).ToArray();
        var writer = new PacketWriter(destination);
        var exceptionThrown = false;

        try
        {
            packet.Write(ref writer);
        }
        catch (InvalidOperationException)
        {
            exceptionThrown = true;
        }

        Assert.True(exceptionThrown);
        Assert.Equal(0, writer.WrittenCount);
        Assert.All(destination, value => Assert.Equal((byte)0xCC, value));
    }

    public static void AssertExactAndOversized(IOutgoingPacket packet, byte[] expected)
    {
        var exact = new byte[packet.Length];
        var exactWriter = new PacketWriter(exact);
        packet.Write(ref exactWriter);

        var oversized = Enumerable.Repeat((byte)0xCC, packet.Length + 2).ToArray();
        var oversizedWriter = new PacketWriter(oversized);
        packet.Write(ref oversizedWriter);

        Assert.Equal(packet.Length, exactWriter.WrittenCount);
        Assert.Equal(packet.Length, oversizedWriter.WrittenCount);
        Assert.Equal(expected, exact);
        Assert.Equal(expected, oversized.AsSpan(0, packet.Length).ToArray());
        Assert.Equal(new byte[] { 0xCC, 0xCC }, oversized.AsSpan(packet.Length).ToArray());
    }
}
