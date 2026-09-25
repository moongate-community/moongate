using Moongate.Core.Geometry;
using Moongate.Network.Packets.Serialization;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Types.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Packets.Characters;

public sealed class CharacterListPacketTests
{
    [Fact]
    public void Constructor_MaximumCityCountAcceptedAndOneMoreRejected()
    {
        var city = CreateCity("Yew", "Inn", new(633, 858, 0), MapType.Trammel, 1075072);

        var maximum = new CharacterListPacket(Slots(5), Enumerable.Repeat(city, 255), CharacterListFlags.None);

        Assert.Equal(11 + 5 * 60 + 255 * 89, maximum.Length);
        Assert.Throws<ArgumentException>(
            () => new CharacterListPacket(Slots(5), Enumerable.Repeat(city, 256), CharacterListFlags.None)
        );
    }

    [Fact]
    public void Constructor_NullCity_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new CharacterListPacket(Slots(5), new StartingCityContent[] { null! }, CharacterListFlags.None)
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Constructor_SupportedSlotCount_ComputesLength(int slotCount)
    {
        var packet = new CharacterListPacket(Slots(slotCount), [], CharacterListFlags.None);

        Assert.Equal(11 + 60 * slotCount, packet.Length);
        Assert.Equal(packet.Length, PacketCodec.Encode(packet).Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Constructor_UnsupportedSlotCount_ThrowsArgumentException(int slotCount)
    {
        Assert.Throws<ArgumentException>(
            () => new CharacterListPacket(Slots(slotCount), [], CharacterListFlags.None)
        );
    }

    [Fact]
    public void Constructor_SnapshotsSlotsAndCities()
    {
        var slots = new List<string?> { "Bob", null, null, null, null };
        var cities = new List<StartingCityContent>
        {
            CreateCity("Yew", "Inn", new(633, 858, 0), MapType.Trammel, 1075072)
        };
        var packet = new CharacterListPacket(slots, cities, CharacterListFlags.None);

        slots.Clear();
        cities.Clear();

        Assert.Equal(5, packet.Characters.Count);
        Assert.Single(packet.Cities);
        Assert.Equal(11 + 5 * 60 + 89, PacketCodec.Encode(packet).Length);
    }

    [Theory]
    [InlineData("name", 31)]
    [InlineData("town", 33)]
    [InlineData("description", 33)]
    public void Constructor_TextTooLong_ThrowsArgumentException(string field, int length)
    {
        var text = new string('A', length);
        var slots = field == "name" ? new List<string?> { text, null, null, null, null } : Slots(5);
        var city = CreateCity(
            field == "town" ? text : "Yew",
            field == "description" ? text : "Inn",
            new(633, 858, 0),
            MapType.Trammel,
            1075072
        );

        Assert.Throws<ArgumentException>(() => new CharacterListPacket(slots, [city], CharacterListFlags.None));
    }

    [Fact]
    public void Constructor_NonAsciiText_ThrowsArgumentException()
    {
        var city = CreateCity("Yew", "Locanda dell'Abbazia è", new(633, 858, 0), MapType.Trammel, 1075072);

        Assert.Throws<ArgumentException>(() => new CharacterListPacket(Slots(5), [city], CharacterListFlags.None));
    }

    [Fact]
    public void Encode_NegativeZAndTerMur_WritesTwosComplementZAndMapId()
    {
        var packet = new CharacterListPacket(
            [null, "A", string.Empty, null, null],
            [CreateCity("Royal City", "Royal City Inn", new(738, 3486, -19), MapType.TerMur, 1150169)],
            CharacterListFlags.None
        );

        var encoded = PacketCodec.Encode(packet);

        Assert.Equal(400, encoded.Length);
        Assert.Equal("A90190", Convert.ToHexString(encoded[..3]));
        Assert.Equal("41", Convert.ToHexString(encoded[64..65]));
        Assert.Equal(
            "000002E200000D9EFFFFFFED0000000500118CD900000000",
            Convert.ToHexString(encoded[370..394])
        );
        Assert.Equal("00000000FFFF", Convert.ToHexString(encoded[394..400]));
    }

    [Fact]
    public void Encode_OneSlotOneCity_MatchesCompleteIndependentFixture()
    {
        var packet = new CharacterListPacket(
            ["Bob"],
            [CreateCity("Yew", "Inn", new(633, 858, 0), MapType.Trammel, 1075072)],
            CharacterListFlags.ContextMenus
        );
        var expected = Convert.FromHexString(
            "A900A0" + "01" +
            "426F62" + Zeros(27) + Zeros(30) +
            "01" +
            "00" +
            "596577" + Zeros(29) +
            "496E6E" + Zeros(29) +
            "00000279" +
            "0000035A" +
            "00000000" +
            "00000001" +
            "00106780" +
            "00000000" +
            "00000008" + "FFFF"
        );

        Assert.Equal(expected, PacketCodec.Encode(packet));
    }

    private static StartingCityContent CreateCity(
        string town,
        string description,
        Point3D location,
        MapType map,
        uint cliloc
    )
    {
        return new()
        {
            Town = town,
            Description = description,
            Location = location,
            Map = map,
            Cliloc = new(cliloc)
        };
    }

    private static List<string?> Slots(int count)
    {
        return Enumerable.Repeat<string?>(null, count).ToList();
    }

    private static string Zeros(int byteCount)
    {
        return new('0', byteCount * 2);
    }
}
