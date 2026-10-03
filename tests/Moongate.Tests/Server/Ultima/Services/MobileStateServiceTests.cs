using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MobileStateServiceTests : IAsyncLifetime
{
    private const long Aria = 2;
    private const long Boris = 3;

    private readonly RecordingWorldViewService _view = new();

    private BroadcastFixture _fixture = null!;
    private MobileStateService _service = null!;
    private MobileEntity _aria = null!;
    private GameSession _ariaSession = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _ariaSession = await _fixture.AddAsync(Aria);
        await _fixture.AddAsync(Boris);
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Aria), out _aria!));
        (_aria.Hits, _aria.HitsMax, _aria.Mana, _aria.ManaMax, _aria.Stamina, _aria.StaminaMax) = (50, 60, 9, 10, 18, 20);
        (_aria.Strength, _aria.Dexterity, _aria.Intelligence) = (60, 20, 10);
        _service = new(_fixture.Mobiles, _fixture.Sessions, _fixture.Sectors, _fixture.Sender, _view, new WorldConfig());
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void SetStats_ChangesWhatIsGiven_AndKeepsTheRest()
    {
        Assert.True(_service.SetStats(_aria, new() { Strength = 80, Mana = 5, Fame = 1200, Karma = -300 }));

        Assert.Equal((80, 20, 10), (_aria.Strength, _aria.Dexterity, _aria.Intelligence));
        Assert.Equal((50, 60, 5, 10, 18, 20), (_aria.Hits, _aria.HitsMax, _aria.Mana, _aria.ManaMax, _aria.Stamina, _aria.StaminaMax));
        Assert.Equal((1200, -300), (_aria.Fame, _aria.Karma));
    }

    [Theory]
    [InlineData(999, 60)]
    [InlineData(-5, 0)]
    public void SetStats_HitPoints_StayBetweenZeroAndTheirMaximum(int asked, int expected)
    {
        Assert.True(_service.SetStats(_aria, new() { Hits = asked, Mana = asked, Stamina = asked }));

        Assert.Equal(expected, _aria.Hits);
        Assert.Equal((Math.Min(expected, 10), Math.Min(expected, 20)), (_aria.Mana, _aria.Stamina));
    }

    [Fact]
    public void SetStats_ALoweredMaximum_TakesWhatIsAboveIt()
    {
        Assert.True(_service.SetStats(_aria, new() { HitsMax = 30, ManaMax = 4, StaminaMax = 100 }));

        Assert.Equal((30, 30, 4, 4, 18, 100), (_aria.Hits, _aria.HitsMax, _aria.Mana, _aria.ManaMax, _aria.Stamina, _aria.StaminaMax));
    }

    [Fact]
    public void SetStats_TheMaximumAndTheValueTogether_UseTheNewMaximum()
    {
        Assert.True(_service.SetStats(_aria, new() { Hits = 100, HitsMax = 120 }));

        Assert.Equal((100, 120), (_aria.Hits, _aria.HitsMax));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65536)]
    public void SetStats_AStatOrAMaximumOutOfRange_ChangesNothing(int value)
    {
        Assert.False(_service.SetStats(_aria, new() { Strength = value, Hits = 1 }));
        Assert.False(_service.SetStats(_aria, new() { HitsMax = value, Dexterity = 99 }));

        Assert.Equal((60, 20, 50, 60), (_aria.Strength, _aria.Dexterity, _aria.Hits, _aria.HitsMax));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void SetStats_SendsThePlayerItsWholeStatus_AndThoseAroundTheHealthBarAsAShare()
    {
        Assert.True(_service.SetStats(_aria, new() { Hits = 30 }));

        var status = Assert.Single(_fixture.Sender.Sent.OfType<MobileStatusPacket>());
        Assert.Equal((false, 30, 60), (status.Compact, status.Status.Hits, status.Status.HitsMax));
        var bar = Assert.Single(_fixture.Sender.Sent.OfType<MobileHitsPacket>());
        Assert.Equal((new Serial((uint)Aria), 50, 100), (bar.Serial, bar.Hits, bar.HitsMax));
        Assert.Equal([Aria, Boris], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void SetStats_WithoutAChangeOfTheHitPoints_TellsOnlyThePlayer()
    {
        Assert.True(_service.SetStats(_aria, new() { Mana = 3, Strength = 61 }));

        Assert.IsType<MobileStatusPacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Equal([Aria], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void SetStats_OfAnNpc_TellsThoseAroundOnly()
    {
        var orc = new MobileEntity
        {
            Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = _aria.Map, Location = _aria.Location, Hits = 40, HitsMax = 40
        };
        _fixture.Mobiles.EnterWorld(orc);

        Assert.True(_service.SetStats(orc, new() { Hits = 10 }));

        Assert.All(_fixture.Sender.Sent, packet => Assert.Equal(25, Assert.IsType<MobileHitsPacket>(packet).Hits));
        Assert.Equal([Aria, Boris], _fixture.Sender.SentSessionIds.Order());
    }

    [Fact]
    public void SetStats_ThoseOutOfSight_AreNotTold()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Boris), out var boris));
        _fixture.Mobiles.MoveTo(boris, boris.Map, new Point3D(3000, 3000, 0));

        Assert.True(_service.SetStats(_aria, new() { Hits = 30 }));

        Assert.Equal([Aria], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void GetSkill_ASkillTheMobileNeverHad_IsZeroWithTheUsualCap()
    {
        var skill = _service.GetSkill(_aria, SkillType.Magery);

        Assert.Equal((SkillType.Magery, 0, 1000, SkillLockType.Up), (skill.Skill, skill.Base, skill.Cap, skill.Lock));
        Assert.Empty(_aria.Skills);
    }

    [Fact]
    public void GetSkills_HasEverySkillOfTheGame_InOrder()
    {
        _aria.Skills.Add(new() { Skill = SkillType.Magery, Base = 505 });

        var skills = _service.GetSkills(_aria);

        Assert.Equal(Enum.GetValues<SkillType>(), skills.Select(skill => skill.Skill));
        Assert.Equal(505, skills[(int)SkillType.Magery].Base);
    }

    [Fact]
    public void SetSkill_SetsItInTenths_AndShowsItInTheSkillWindow()
    {
        Assert.True(_service.SetSkill(_aria, SkillType.Magery, 505));

        var known = Assert.Single(_aria.Skills);
        Assert.Equal((SkillType.Magery, 505, 1000), (known.Skill, known.Base, known.Cap));
        var packet = Assert.IsType<SkillsPacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Same(known, Assert.Single(packet.Skills));
        Assert.Equal([Aria], _fixture.Sender.SentSessionIds);
    }

    [Theory]
    [InlineData(1500, null, 1000, 1000)]
    [InlineData(-10, null, 0, 1000)]
    [InlineData(1150, 1200, 1150, 1200)]
    [InlineData(900, 800, 800, 800)]
    public void SetSkill_StaysBetweenZeroAndItsCap(int value, int? cap, int expected, int expectedCap)
    {
        Assert.True(_service.SetSkill(_aria, SkillType.Magery, value, cap));

        Assert.Equal((expected, expectedCap), (_aria.Skills[0].Base, _aria.Skills[0].Cap));
    }

    [Fact]
    public void SetSkill_AnUnknownSkillOrABadCap_ChangesNothing()
    {
        Assert.False(_service.SetSkill(_aria, (SkillType)200, 500));
        Assert.False(_service.SetSkill(_aria, SkillType.Magery, 500, -1));
        Assert.False(_service.SetSkill(_aria, SkillType.Magery, 500, 70000));

        Assert.Empty(_aria.Skills);
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void SetName_RenamesIt_AndTellsItsPlayerAndThoseAround()
    {
        Assert.True(_service.SetName(_aria, "  Aria the Brave "));

        Assert.Equal("Aria the Brave", _aria.Name);
        Assert.Equal("Aria the Brave", Assert.Single(_fixture.Sender.Sent.OfType<MobileStatusPacket>()).Status.Name);
        Assert.Equal([$"MobileAppeared {Aria}"], _view.Calls);
    }

    [Theory, InlineData(""), InlineData("   ")]
    public void SetName_ABlankName_IsRefused(string name)
    {
        Assert.False(_service.SetName(_aria, name));

        Assert.Equal("Player", _aria.Name);
    }

    [Fact]
    public void SetLooks_ChangesTheBodyAndTheHue_AndShowsThemToItsPlayerAndThoseAround()
    {
        Assert.True(_service.SetLooks(_aria, 0x3A, 0x0481));

        Assert.Equal((0x3A, (ushort)0x0481), (_aria.Body, _aria.SkinHue.Value));
        var update = Assert.Single(_fixture.Sender.Sent.OfType<MobileUpdatePacket>());
        Assert.Equal(new Serial((uint)Aria), update.Serial);
        Assert.Equal([$"MobileAppeared {Aria}"], _view.Calls);
    }

    [Fact]
    public void SetLooks_OnlyOneOfTheTwo_KeepsTheOther()
    {
        _aria.Body = 400;

        Assert.True(_service.SetLooks(_aria, null, 5));

        Assert.Equal((400, (ushort)5), (_aria.Body, _aria.SkinHue.Value));
    }

    [Theory, InlineData(-1, null), InlineData(70000, null), InlineData(null, -1), InlineData(null, 70000)]
    public void SetLooks_OutOfRange_ChangesNothing(int? body, int? hue)
    {
        _aria.Body = 400;

        Assert.False(_service.SetLooks(_aria, body, hue));

        Assert.Equal((400, (ushort)0), (_aria.Body, _aria.SkinHue.Value));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void SendStatus_OfAnotherMobile_IsCompact_AndOfTheOwnCharacterWhole()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Boris), out var boris));

        _service.SendStatus(_ariaSession, boris);
        _service.SendStatus(_ariaSession, _aria);

        Assert.Equal([true, false], _fixture.Sender.Sent.Cast<MobileStatusPacket>().Select(packet => packet.Compact));
    }

    [Fact]
    public void SendSkills_SendsTheWholeList()
    {
        _service.SendSkills(_ariaSession, _aria);

        Assert.Equal(Enum.GetValues<SkillType>().Length, Assert.IsType<SkillsPacket>(Assert.Single(_fixture.Sender.Sent)).Skills.Count);
    }
}
