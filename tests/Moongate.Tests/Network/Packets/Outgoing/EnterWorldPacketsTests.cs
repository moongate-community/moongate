using System.Buffers.Binary;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Data;
using Moongate.Network.Packets.Outgoing;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Network.Packets.Outgoing;

public class EnterWorldPacketsTests
{
    private static byte[] Serialize<TPacket>(TPacket packet) where TPacket : Moongate.Network.Interfaces.IOutgoingPacket
    {
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }

    [Fact]
    public void LoginConfirm_IsByteExact()
    {
        var b = Serialize(
            new LoginConfirmPacket(new Serial(0x12345678), 0x0190, 1000, 2000, 5, DirectionType.South, 7168, 4096)
        );

        Assert.Equal(37, b.Length);
        Assert.Equal(0x1B, b[0]);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(1)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(5)));
        Assert.Equal(0x0190, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(9)));
        Assert.Equal(1000, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(11)));
        Assert.Equal(2000, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(13)));
        Assert.Equal(5, BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(15)));
        Assert.Equal((byte)DirectionType.South, b[17]);
        Assert.Equal(-1, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(19)));
        Assert.Equal(7168, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(27)));
        Assert.Equal(4096, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(29)));
    }

    [Fact]
    public void MapChange_IsByteExact()
    {
        var b = Serialize(new MapChangePacket(MapType.Trammel));

        Assert.Equal(6, b.Length);
        Assert.Equal(0xBF, b[0]);
        Assert.Equal(6, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(1)));
        Assert.Equal(0x08, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(3)));
        Assert.Equal(1, b[5]);
    }

    [Fact]
    public void MapPatches_IsByteExactAndZeroed()
    {
        var b = Serialize(new MapPatchesPacket());

        Assert.Equal(41, b.Length);
        Assert.Equal(0xBF, b[0]);
        Assert.Equal(41, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(1)));
        Assert.Equal(0x18, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(3)));
        Assert.Equal(4, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(5)));
        Assert.All(b.AsSpan(9, 32).ToArray(), x => Assert.Equal(0, x)); // all patch counts zero
    }

    [Fact]
    public void SeasonChange_IsByteExact()
    {
        var b = Serialize(new SeasonChangePacket(2, true));

        Assert.Equal(new byte[] { 0xBC, 2, 1 }, b);
    }

    [Fact]
    public void MobileUpdate_IsByteExact()
    {
        var b = Serialize(
            new MobileUpdatePacket(new Serial(0xABCD), 0x0191, new Hue(0x0203), 0x02, 1234, 5678, -7, DirectionType.West)
        );

        Assert.Equal(19, b.Length);
        Assert.Equal(0x20, b[0]);
        Assert.Equal(0xABCDu, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(1)));
        Assert.Equal(0x0191, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(5)));
        Assert.Equal(0, b[7]);
        Assert.Equal(0x0203, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(8)));
        Assert.Equal(0x02, b[10]);
        Assert.Equal(1234, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(11)));
        Assert.Equal(5678, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(13)));
        Assert.Equal((byte)DirectionType.West, b[17]);
        Assert.Equal(-7, (sbyte)b[18]);
    }

    [Fact]
    public void OverallLightLevel_IsByteExact()
        => Assert.Equal(new byte[] { 0x4F, 3 }, Serialize(new OverallLightLevelPacket(3)));

    [Fact]
    public void PersonalLightLevel_IsByteExact()
    {
        var b = Serialize(new PersonalLightLevelPacket(new Serial(0x99), 4));

        Assert.Equal(6, b.Length);
        Assert.Equal(0x4E, b[0]);
        Assert.Equal(0x99u, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(1)));
        Assert.Equal(4, b[5]);
    }

    [Fact]
    public void WarMode_IsByteExact()
        => Assert.Equal(new byte[] { 0x72, 1, 0x00, 0x32, 0x00 }, Serialize(new WarModePacket(true)));

    [Fact]
    public void LoginComplete_IsSingleByte()
        => Assert.Equal(new byte[] { 0x55 }, Serialize(new LoginCompletePacket()));

    [Fact]
    public void GameTime_IsByteExact()
        => Assert.Equal(new byte[] { 0x5B, 12, 30, 45 }, Serialize(new GameTimePacket(12, 30, 45)));

    [Fact]
    public void MobileIncoming_NoItems_HasTerminatorAndLength()
    {
        var b = Serialize(
            new MobileIncomingPacket(
                new Serial(0x01),
                0x0190,
                100,
                200,
                3,
                DirectionType.North,
                new Hue(0),
                0,
                NotorietyType.Innocent,
                []
            )
        );

        Assert.Equal(23, b.Length);
        Assert.Equal(0x78, b[0]);
        Assert.Equal(23, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(1)));
        Assert.Equal(1, b[18]);                                        // notoriety
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(19))); // terminator
    }

    [Fact]
    public void MobileIncoming_WithItems_WritesEachEntry()
    {
        var items = new MobileIncomingItem[]
        {
            new(new Serial(0x40000001), 0x1B72, LayerType.Shirt, new Hue(0x0A)),
            new(new Serial(0x7FFFFFFF), 0x203B, LayerType.Hair, new Hue(0x0B))
        };

        var b = Serialize(
            new MobileIncomingPacket(
                new Serial(0x05),
                0x0190,
                0,
                0,
                0,
                DirectionType.North,
                new Hue(0),
                0,
                NotorietyType.Innocent,
                items
            )
        );

        Assert.Equal(23 + 9 * 2, b.Length);
        Assert.Equal(23 + 9 * 2, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(1)));

        Assert.Equal(0x40000001u, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(19)));
        Assert.Equal(0x1B72, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(23)));
        Assert.Equal((byte)LayerType.Shirt, b[25]);
        Assert.Equal(0x0A, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(26)));

        Assert.Equal(0x7FFFFFFFu, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(28)));
        Assert.Equal((byte)LayerType.Hair, b[34]);

        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(37))); // terminator
    }

    [Fact]
    public void MobileStatus_IsVersion6WithStatsAndName()
    {
        var b = Serialize(
            new MobileStatusPacket(
                new Serial(0x07),
                "Squid",
                40,
                50,
                Female: true,
                Strength: 50,
                Dexterity: 45,
                Intelligence: 25,
                Stamina: 44,
                StaminaMax: 45,
                Mana: 25,
                ManaMax: 25,
                Race: RaceType.Human,
                StatCap: 225,
                FollowersMax: 5
            )
        );

        Assert.Equal(121, b.Length);
        Assert.Equal(0x11, b[0]);
        Assert.Equal(121, BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(1)));
        Assert.Equal(0x07u, BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(3)));
        Assert.Equal("Squid", Encoding.ASCII.GetString(b, 7, 30).TrimEnd('\0'));
        Assert.Equal(40, BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(37))); // current hits
        Assert.Equal(50, BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(39))); // max hits
        Assert.Equal(0, b[41]);                                              // name-change flag
        Assert.Equal(6, b[42]);                                              // version
        Assert.Equal(1, b[43]);                                             // female
        Assert.Equal(50, BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(44))); // strength
        Assert.Equal(1, b[68]);                                             // race id (Human 0 + 1)
    }
}
