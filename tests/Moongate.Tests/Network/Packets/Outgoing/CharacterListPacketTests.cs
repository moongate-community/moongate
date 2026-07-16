using System.Buffers.Binary;
using System.Text;
using Moongate.Network.Packets.Outgoing;
using Moongate.UO.Data.StartingCities;
using Moongate.UO.Data.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class CharacterListPacketTests
{
    [Fact]
    public void Write_ExtendedFormat_IsByteExact()
    {
        var city = new StartingCity
        {
            City = "Britain",
            Building = "Castle British",
            Description = 1075072,
            X = 1495,
            Y = 1629,
            Z = 10,
            Map = MapType.Trammel
        };
        var packet = new CharacterListPacket(new[] { "Squid" }, new[] { city }, 2, CharacterListFlagType.Modern);

        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);
        var b = writer.Span.ToArray();

        Assert.Equal(0xA9, b[0]);
        Assert.Equal(220, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(1))); // 11 + 60*2 + 89*1
        Assert.Equal(220, b.Length);
        Assert.Equal(2, b[3]);                                                   // slot count
        Assert.Equal("Squid", Encoding.ASCII.GetString(b, 4, 30).TrimEnd('\0')); // slot 0 name
        Assert.All(b.AsSpan(34, 90).ToArray(), x => Assert.Equal(0, x));         // slot 0 password + empty slot 1
        Assert.Equal(1, b[124]);                                                 // city count
        Assert.Equal(0, b[125]);                                                 // city index
        Assert.Equal("Britain", Encoding.ASCII.GetString(b, 126, 32).TrimEnd('\0'));
        Assert.Equal("Castle British", Encoding.ASCII.GetString(b, 158, 32).TrimEnd('\0'));
        Assert.Equal(1495, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(190)));
        Assert.Equal(1629, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(194)));
        Assert.Equal(10, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(198)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(202))); // Trammel = 1
        Assert.Equal(1075072, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(206)));
        Assert.Equal(0x11E8, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(214))); // flags
        Assert.Equal(-1, BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(218)));     // trailing
    }
}
