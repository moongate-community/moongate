using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing;
using SquidStd.Network.Spans;
using Xunit.Abstractions;

namespace Moongate.Tests.Network;

/// <summary>
/// Holds the world-item packet to ModernUO's encoding, byte for byte. A drift here is invisible to
/// every server-side test and shows up only as a client silently drawing nothing — which is exactly
/// how a full day was lost before this file existed.
/// </summary>
public class WorldItemPacketTests
{
    private readonly ITestOutputHelper _output;

    public WorldItemPacketTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Write_MatchesModernUOByteForByte()
    {
        // A metal door at the New Haven bank: serial 0x40000123, graphic 0x675, amount 1,
        // at 3508,2554,20, hue 0.
        var packet = new WorldItemPacket((Serial)0x40000123u, 0x675, 1, new(3508, 2554, 20), new(0));

        var writer = new SpanWriter(64, true);
        packet.Write(ref writer);
        var ours = writer.Span.ToArray();

        // ModernUO CreateWorldItem, transcribed byte for byte from
        // others/ModernUO/Projects/Server/Network/Packets/OutgoingEntityPackets.cs.
        var reference = new List<byte> { 0xF3 };
        reference.AddRange(new byte[] { 0x00, 0x01 });                    // ushort 0x1
        reference.Add(0x00);                                              // item (not multi)
        reference.AddRange(new byte[] { 0x40, 0x00, 0x01, 0x23 });        // serial BE
        reference.AddRange(new byte[] { 0x06, 0x75 });                    // graphic BE
        reference.Add(0x00);                                              // graphic increment
        reference.AddRange(new byte[] { 0x00, 0x01 });                    // amount
        reference.AddRange(new byte[] { 0x00, 0x01 });                    // amount again
        reference.AddRange(new byte[] { 0x0D, 0xB4 });                    // x 3508 & 0x7FFF
        reference.AddRange(new byte[] { 0x09, 0xFA });                    // y 2554 & 0x3FFF
        reference.Add(0x14);                                              // z 20
        reference.Add(0x00);                                              // light
        reference.AddRange(new byte[] { 0x00, 0x00 });                    // hue
        reference.Add(0x00);                                              // flags
        reference.AddRange(new byte[] { 0x00, 0x00 });                    // trailer

        _output.WriteLine($"ours ({ours.Length}):      " + Convert.ToHexString(ours));
        _output.WriteLine($"reference ({reference.Count}): " + Convert.ToHexString(reference.ToArray()));

        Assert.Equal(Convert.ToHexString(reference.ToArray()), Convert.ToHexString(ours));
    }
}
