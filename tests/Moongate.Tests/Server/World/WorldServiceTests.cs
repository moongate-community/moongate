using System.Buffers.Binary;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Server.World;

public class WorldServiceTests
{
    private static WorldService Service(StubItemService items, TimeProvider? time = null)
        => new(items, new StubEventBus(), time ?? TimeProvider.System);

    private static byte[] Serialize(IOutgoingPacket packet)
    {
        var writer = new SpanWriter(256, true);
        packet.Write(ref writer);

        return writer.Span.ToArray();
    }

    private static MobileEntity Player()
        => new()
        {
            Id = new Serial(0x64),
            Name = "Hero",
            Body = 0x0190,
            MapId = 1,
            Position = new Point3D(1000, 2000, 5),
            Direction = DirectionType.South,
            SkinHue = new Hue(0x83EA),
            Gender = GenderType.Male,
            Race = RaceType.Human,
            Strength = 50,
            Dexterity = 45,
            Intelligence = 25,
            Hits = 50,
            HitsMax = 50,
            Stamina = 45,
            StaminaMax = 45,
            Mana = 25,
            ManaMax = 25,
            HairStyle = 0x203B,
            HairHue = new Hue(0x44)
        };

    [Fact]
    public void BuildSequence_EmitsExpectedOrderedOpcodes()
    {
        var opcodes = Service(new StubItemService([]))
            .BuildSequence(Player())
            .Select(packet => Serialize(packet)[0])
            .ToArray();

        Assert.Equal(
            // The second 0x11 neighbour is 0xBF/0x19, the stat lock state paired with the status.
            new byte[] { 0x1B, 0xBF, 0xBF, 0xBC, 0xB9, 0x20, 0x4F, 0x4E, 0x78, 0x11, 0xBF, 0x72, 0x55, 0x5B },
            opcodes
        );
    }

    [Fact]
    public void BuildSequence_SeasonComesFromTheMap()
    {
        // The player stands on Trammel (map 1), whose definition season is Spring (0).
        var season = Serialize(Service(new StubItemService([])).BuildSequence(Player())[3]); // 0xBC is the 4th packet

        Assert.Equal(0xBC, season[0]);
        Assert.Equal((byte)SeasonType.Spring, season[1]);
    }

    [Fact]
    public void BuildSequence_GameTimeComesFromTheTimeProvider()
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 13, 37, 45, TimeSpan.Zero));

        var gameTime = Serialize(Service(new StubItemService([]), time).BuildSequence(Player())[13]); // 0x5B is last

        Assert.Equal(new byte[] { 0x5B, 13, 37, 45 }, gameTime);
    }

    [Fact]
    public void BuildSequence_WarModeComesFromTheMobile()
    {
        var mobile = Player();
        mobile.Warmode = true;

        var warMode = Serialize(Service(new StubItemService([])).BuildSequence(mobile)[11]); // 0x72 is the 12th packet

        Assert.Equal(0x72, warMode[0]);
        Assert.Equal(1, warMode[1]);
    }

    [Theory]
    [InlineData(0, false, NotorietyType.Innocent)]
    [InlineData(0, true, NotorietyType.Criminal)]
    [InlineData(5, false, NotorietyType.Murderer)]
    public void BuildSequence_NotorietyDerivesFromKillsAndCriminal(int kills, bool criminal, NotorietyType expected)
    {
        var mobile = Player();
        mobile.Kills = kills;
        mobile.Criminal = criminal;

        var incoming = Serialize(Service(new StubItemService([])).BuildSequence(mobile)[8]); // 0x78 is the 9th packet

        Assert.Equal(0x78, incoming[0]);
        Assert.Equal((byte)expected, incoming[18]);
    }

    [Fact]
    public void BuildSequence_StatusCarriesStatCapAndFollowersFromTheMobile()
    {
        var mobile = Player();
        mobile.StatCap = 250;
        mobile.Followers = 3;
        mobile.FollowersMax = 6;

        var status = Serialize(Service(new StubItemService([])).BuildSequence(mobile)[9]); // 0x11 is the 10th packet

        Assert.Equal(0x11, status[0]);
        Assert.Equal(250, BinaryPrimitives.ReadInt16BigEndian(status.AsSpan(69)));
        Assert.Equal(3, status[71]);
        Assert.Equal(6, status[72]);
    }

    [Fact]
    public void BuildSequence_StatusDefaultsToTheClassicStatCapAndFollowersMax()
    {
        var status = Serialize(Service(new StubItemService([])).BuildSequence(Player())[9]);

        Assert.Equal(225, BinaryPrimitives.ReadInt16BigEndian(status.AsSpan(69)));
        Assert.Equal(0, status[71]);
        Assert.Equal(5, status[72]);
    }

    [Fact]
    public void BuildSequence_StatLockInfoCarriesTheMobileLocks()
    {
        var mobile = Player();
        mobile.StrengthLock = StatLockType.Locked;   // 2
        mobile.DexterityLock = StatLockType.Down;    // 1
        mobile.IntelligenceLock = StatLockType.Up;   // 0

        var locks = Serialize(Service(new StubItemService([])).BuildSequence(mobile)[10]); // 0xBF/0x19 follows 0x11

        Assert.Equal(0xBF, locks[0]);
        Assert.Equal(12, BinaryPrimitives.ReadUInt16BigEndian(locks.AsSpan(1)));
        Assert.Equal(0x19, BinaryPrimitives.ReadUInt16BigEndian(locks.AsSpan(3)));
        Assert.Equal(0x64u, BinaryPrimitives.ReadUInt32BigEndian(locks.AsSpan(6)));

        // Two bits per stat: (Str << 4) | (Dex << 2) | Int.
        Assert.Equal((2 << 4) | (1 << 2) | 0, locks[11]);
    }

    [Fact]
    public void BuildSequence_MobileIncoming_IncludesEquipmentAndHair()
    {
        var shirt = new ItemEntity
        {
            Id = new Serial(0x40000005),
            ItemId = 0x1517,
            Hue = new Hue(0x10),
            EquippedLayer = LayerType.Shirt
        };
        var incoming = Serialize(Service(new StubItemService([shirt])).BuildSequence(Player())[8]); // 0x78 is the 9th packet

        Assert.Equal(0x78, incoming[0]);
        Assert.Equal(23 + 9 * 2, BinaryPrimitives.ReadUInt16BigEndian(incoming.AsSpan(1))); // shirt + hair
        Assert.Equal(0x40000005u, BinaryPrimitives.ReadUInt32BigEndian(incoming.AsSpan(19)));
        Assert.Equal((byte)LayerType.Shirt, incoming[25]);
        Assert.Equal(0x7FFFFFFFu, BinaryPrimitives.ReadUInt32BigEndian(incoming.AsSpan(28))); // hair virtual serial
        Assert.Equal((byte)LayerType.Hair, incoming[34]);
    }
}
