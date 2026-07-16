using System.Buffers.Binary;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Interfaces;
using Moongate.Persistence.Entities;
using Moongate.Server.Interfaces.Mobiles;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using Moongate.Ultima.Types;
using SquidStd.Network.Spans;

namespace Moongate.Tests.Server.World;

public class WorldServiceTests
{
    private static WorldService Service(StubItemService items, TimeProvider? time = null, ISkillService? skills = null)
        => new(items, skills ?? Skills(), new VirtualSerialService(), new StubEventBus(), time ?? TimeProvider.System);

    // Three skills is enough to prove the list is built from the registry rather than from the mobile.
    private static SkillService Skills()
    {
        var skills = new SkillService();
        skills.Register(new() { Id = 0, Name = "Alchemy" });
        skills.Register(new() { Id = 1, Name = "Anatomy" });
        skills.Register(new() { Id = 40, Name = "Swordsmanship" });

        return skills;
    }

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
            new byte[] { 0x1B, 0xBF, 0xBF, 0xBC, 0xB9, 0x20, 0x4F, 0x4E, 0x78, 0x11, 0xBF, 0x3A, 0x72, 0x55, 0x5B },
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

        var gameTime = Serialize(Service(new StubItemService([]), time).BuildSequence(Player())[14]); // 0x5B is last

        Assert.Equal(new byte[] { 0x5B, 13, 37, 45 }, gameTime);
    }

    [Fact]
    public void BuildSequence_WarModeComesFromTheMobile()
    {
        var mobile = Player();
        mobile.Warmode = true;

        var warMode = Serialize(Service(new StubItemService([])).BuildSequence(mobile)[12]); // 0x72 is the 13th packet

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
    public void BuildSequence_SkillsCarryEveryRegisteredSkillEvenUntrainedOnes()
    {
        var mobile = Player();
        mobile.Skills[40] = new MobileSkill { Value = 733, Cap = 1200, Lock = SkillLockType.Locked };

        var skills = Serialize(Service(new StubItemService([])).BuildSequence(mobile)[11]); // 0x3A follows the locks

        Assert.Equal(0x3A, skills[0]);
        Assert.Equal(6 + 9 * 3, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(1))); // all three, not just the trained one
        Assert.Equal(0x02, skills[3]);                                                    // absolute, with caps

        // First entry: Alchemy (id 0) untrained, still sent one-based, with the default cap and lock.
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(4)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(6)));
        Assert.Equal((byte)SkillLockType.Up, skills[10]);
        Assert.Equal(1000, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(11)));

        // Third entry: Swordsmanship (id 40 -> 41), value, base and the mobile's own cap and lock.
        Assert.Equal(41, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(22)));
        Assert.Equal(733, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(24)));  // value
        Assert.Equal(733, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(26)));  // base
        Assert.Equal((byte)SkillLockType.Locked, skills[28]);                        // lock from the mobile
        Assert.Equal(1200, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(29))); // cap from the mobile

        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(skills.AsSpan(31))); // terminator
    }

    [Fact]
    public void BuildSequence_TwoItemsOnOneLayer_OnlyTheFirstIsDrawn()
    {
        // The client cannot render two things on one slot; ModernUO keeps the first and drops the rest.
        var shirt = new ItemEntity
        {
            Id = new Serial(0x40000005), ItemId = 0x1517, EquippedLayer = LayerType.Shirt
        };
        var otherShirt = new ItemEntity
        {
            Id = new Serial(0x40000006), ItemId = 0x1518, EquippedLayer = LayerType.Shirt
        };

        var incoming = Serialize(Service(new StubItemService([shirt, otherShirt])).BuildSequence(Player())[8]);

        // Shirt + hair, not shirt + shirt + hair.
        Assert.Equal(23 + 9 * 2, BinaryPrimitives.ReadUInt16BigEndian(incoming.AsSpan(1)));
        Assert.Equal(0x40000005u, BinaryPrimitives.ReadUInt32BigEndian(incoming.AsSpan(19)));
    }

    [Fact]
    public void BuildSequence_ItemAlreadyOnTheHairLayer_HairIsNotDrawn()
    {
        // A real item holding the hair layer wins over the mobile's hairstyle.
        var wig = new ItemEntity
        {
            Id = new Serial(0x40000007), ItemId = 0x203C, EquippedLayer = LayerType.Hair
        };

        var incoming = Serialize(Service(new StubItemService([wig])).BuildSequence(Player())[8]);

        // Just the wig: the mobile's own hair must not be appended on top of it.
        Assert.Equal(23 + 9 * 1, BinaryPrimitives.ReadUInt16BigEndian(incoming.AsSpan(1)));
        Assert.Equal(0x40000007u, BinaryPrimitives.ReadUInt32BigEndian(incoming.AsSpan(19)));
        Assert.Equal((byte)LayerType.Hair, incoming[25]);
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

        // Hair rides along as a pseudo-item on a serial from the reserved virtual band, since it is not
        // an entity the server owns.
        Assert.True(new Serial(BinaryPrimitives.ReadUInt32BigEndian(incoming.AsSpan(28))).IsVirtual);
        Assert.Equal((byte)LayerType.Hair, incoming[34]);
    }
}
