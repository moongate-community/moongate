using Moongate.Tests.TestSupport.Ultima.Weight;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Movement;
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
    private readonly StubWeightService _weight = new() { CarriedStones = 37, MaximumStones = 215 };
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
        _service = new(
            _fixture.Mobiles,
            _fixture.Sessions,
            _fixture.Sectors,
            _fixture.Sender,
            _view,
            new WorldConfig(),
            new Lazy<IWeightService>(() => _weight)
        );
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
    public void SetStats_TheHitPoints_GoToThePlayerAsTheyAre_AndToThoseAroundAsAShare()
    {
        Assert.True(_service.SetStats(_aria, new() { Hits = 30 }));

        var bars = _fixture.Sender.Sent.Cast<MobileHitsPacket>().ToList();
        Assert.Equal([(30, 60), (50, 100)], bars.Select(bar => (bar.Hits, bar.HitsMax)));
        Assert.Equal([Aria, Boris], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void SetStats_TheManaAndTheStamina_GoToThePlayerOnly()
    {
        Assert.True(_service.SetStats(_aria, new() { Mana = 3, StaminaMax = 40 }));

        Assert.Equal([typeof(MobileManaPacket), typeof(MobileStaminaPacket)], _fixture.Sender.Sent.Select(packet => packet.GetType()));
        var mana = (MobileManaPacket)_fixture.Sender.Sent[0];
        var stamina = (MobileStaminaPacket)_fixture.Sender.Sent[1];
        Assert.Equal((3, 10, 18, 40), (mana.Mana, mana.ManaMax, stamina.Stamina, stamina.StaminaMax));
        Assert.Equal([Aria, Aria], _fixture.Sender.SentSessionIds);
    }

    [Theory]
    [InlineData("strength")]
    [InlineData("fame")]
    public void SetStats_AStatTheFameOrTheKarma_SendsThePlayerItsWholeStatus(string what)
    {
        var change = what == "strength" ? new MobileStatsChange { Strength = 61, Mana = 3 } : new MobileStatsChange { Fame = 5, Mana = 3 };

        Assert.True(_service.SetStats(_aria, change));

        var status = Assert.IsType<MobileStatusPacket>(Assert.Single(_fixture.Sender.Sent));
        Assert.Equal((false, 3), (status.Compact, status.Status.Mana));
        Assert.Equal([Aria], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void SetStats_ThatChangeNothing_SendNothing()
    {
        Assert.True(_service.SetStats(_aria, new() { Hits = 50, Strength = 60 }));

        Assert.Empty(_fixture.Sender.Sent);
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
        Assert.Equal(30, Assert.IsType<MobileHitsPacket>(Assert.Single(_fixture.Sender.Sent)).Hits);
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
        // The status carries the name; the figure did not change, so the client is not placed again.
        Assert.Equal("Aria the Brave", Assert.IsType<MobileStatusPacket>(Assert.Single(_fixture.Sender.Sent)).Status.Name);
        Assert.Equal([$"MobileAppeared {Aria}"], _view.Calls);
    }

    // The status shows 30 characters and the world keeps 255: a longer name would fail every save.
    [Fact]
    public void SetName_ANameLongerThanTheStatusShows_IsRefused()
    {
        Assert.True(_service.SetName(_aria, new string('x', 30)));
        Assert.False(_service.SetName(_aria, new string('y', 31)));

        Assert.Equal(new string('x', 30), _aria.Name);
    }

    // The client starts its step sequence again when it gets 0x20: the server must expect it from the start too.
    [Fact]
    public async Task SetLooks_StartsTheStepSequenceOfItsPlayerAgain()
    {
        var steps = new MovementState { ExpectedSequence = 7, NextStepAt = 123_456 };
        await _fixture.Network.ExecuteOnLoopAsync(() => _ariaSession.Set(MovementSessionKeys.State, steps));

        Assert.True(_service.SetLooks(_aria, 0x3A, null));

        Assert.Equal(((byte)0, 0L), (steps.ExpectedSequence, steps.NextStepAt));
    }

    [Fact]
    public void SetSkill_ThatChangesNothing_SendsNothing()
    {
        Assert.True(_service.SetSkill(_aria, SkillType.Magery, 505));
        _fixture.Sender.Sent.Clear();

        Assert.True(_service.SetSkill(_aria, SkillType.Magery, 505));
        Assert.True(_service.SetSkill(_aria, SkillType.Magery, 505, 1000));

        Assert.Empty(_fixture.Sender.Sent);
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
        // The status shows neither.
        var update = Assert.IsType<MobileUpdatePacket>(Assert.Single(_fixture.Sender.Sent));
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
    public void SetHidden_HidesAndReveals_AndTellsTheViewOnceForEachChange()
    {
        _service.SetHidden(_aria, true);
        _service.SetHidden(_aria, true);
        Assert.True(_aria.Hidden);

        _service.SetHidden(_aria, false);

        Assert.False(_aria.Hidden);
        Assert.Equal([$"HiddenChanged {Aria}", $"HiddenChanged {Aria}"], _view.Calls);
    }

    [Fact]
    public void SetFrozen_FreezesAndFrees_AndTellsTheViewOnceForEachChange()
    {
        _service.SetFrozen(_aria, true);
        _service.SetFrozen(_aria, true);
        Assert.True(_aria.Frozen);

        _service.SetFrozen(_aria, false);

        Assert.Equal([$"FlagsChanged {Aria}", $"FlagsChanged {Aria}"], _view.Calls);
    }

    // The client waits for the answer to its request also when the mode is the one it already had.
    [Fact]
    public void SetWarMode_AlwaysAnswersThePlayer_AndTellsTheViewOnlyOfAChange()
    {
        _service.SetWarMode(_aria, true);
        _service.SetWarMode(_aria, true);

        Assert.True(_aria.WarMode);
        Assert.Equal([true, true], _fixture.Sender.Sent.Cast<WarModePacket>().Select(packet => packet.WarMode));
        Assert.Equal([Aria, Aria], _fixture.Sender.SentSessionIds);
        Assert.Equal([$"FlagsChanged {Aria}"], _view.Calls);
    }

    [Fact]
    public void SetStats_OfAHiddenMobile_DoesNotShowItsHealthBarToThePlayersAround()
    {
        _aria.Hidden = true;

        Assert.True(_service.SetStats(_aria, new() { Hits = 30 }));

        Assert.Equal([Aria], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public async Task SetStats_OfAHiddenMobile_ShowsItsHealthBarToTheStaffAround()
    {
        _aria.Hidden = true;
        Assert.True(_fixture.Sessions.TryGetByCharacterId(new Serial((uint)Boris), out var boris));
        await _fixture.Network.ExecuteOnLoopAsync(() => boris.Set(SessionKeys.AccountType, AccountType.GameMaster));

        Assert.True(_service.SetStats(_aria, new() { Hits = 30 }));

        Assert.Equal([Aria, Boris], _fixture.Sender.SentSessionIds);
    }

    [Fact]
    public void SendStatus_OfAHiddenMobile_IsNotForAPlayer()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial((uint)Boris), out var boris));
        boris.Hidden = true;
        _aria.Hidden = true;

        _service.SendStatus(_ariaSession, boris);
        Assert.Empty(_fixture.Sender.Sent);

        // Its own player still gets it.
        _service.SendStatus(_ariaSession, _aria);
        Assert.Single(_fixture.Sender.Sent);
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
    public void SendStatus_OfTheOwnCharacter_CarriesWhatItCarriesAndMayCarry()
    {
        _service.SendStatus(_ariaSession, _aria);

        var status = Assert.IsType<MobileStatusPacket>(Assert.Single(_fixture.Sender.Sent)).Status;
        Assert.Equal((37, 215), (status.Weight, status.MaxWeight));
    }

    [Fact]
    public void SendSkills_SendsTheWholeList()
    {
        _service.SendSkills(_ariaSession, _aria);

        Assert.Equal(Enum.GetValues<SkillType>().Length, Assert.IsType<SkillsPacket>(Assert.Single(_fixture.Sender.Sent)).Skills.Count);
    }
}
