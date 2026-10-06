using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.Combat;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Death;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Skills;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

/// <summary>
///     Melee combat: a player and an orc side by side, a swing every 2.5 seconds at 100 stamina. Rolls the test does not
///     queue are 0.999 and the dice give 0.
/// </summary>
public sealed class CombatServiceTests : IAsyncLifetime
{
    private const int Orc = 0x100;
    private const int HumanBody = 400;
    private const int OrcBody = 17;
    private const int OrcHurt = 0x1B2;
    private const int OrcAttack = 0x1B0;

    private readonly RecordingMobileStateService _state = new() { Apply = true };
    private readonly StubSkillService _skills = new();
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubDeathService _death = new();
    private readonly RecordingCrimeService _crimes = new();
    private readonly StubLineOfSightService _sight = new();
    private readonly RecordingTimerService _timers = new();
    private readonly CombatConfig _config = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _clock = new();

    private BroadcastFixture _fixture = null!;
    private MobileEntity _aria = null!;
    private MobileEntity _orc = null!;
    private CombatService _combat = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        (_aria.Hits, _aria.HitsMax, _aria.Stamina, _aria.Body) = (30, 30, 100, HumanBody);
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Tactics, Base = 500 });
        _orc = new()
        {
            Id = new Serial(Orc), Name = "an orc", TemplateId = "orc", Body = OrcBody, Map = MapType.Trammel,
            Location = new Point3D(1, 0, 0), Hits = 30, HitsMax = 30, Stamina = 100, Notoriety = NotorietyType.Enemy
        };
        _orc.Skills.Add(new MobileSkill { Skill = SkillType.Tactics, Base = 500 });
        _fixture.Mobiles.EnterWorld(_orc);
        _combat = new(
            _fixture.Mobiles,
            _state,
            new MobileTemplateService(
                new StubDataLoaderService().With(
                    new MobileTemplate
                    {
                        Id = "orc", Damage = DiceSpec.FromValue(8), Sounds = new MobileSounds { Attack = OrcAttack, Hurt = OrcHurt }
                    }
                )
            ),
            _skills,
            _fixture.Sessions,
            _fixture.Sender,
            _view,
            _speech,
            _death,
            _crimes,
            _sight,
            new StubDataLoaderService().With(
                new BodyContent { Body = new Body(HumanBody), Type = BodyType.Human },
                new BodyContent { Body = new Body(OrcBody), Type = BodyType.Monster }
            ),
            _timers,
            _config,
            new WorldConfig(),
            _clock,
            _random
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_RegistersATimerOfATenthOfASecond_AndStopAsyncTakesItAway()
    {
        await _combat.StartAsync();

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal((CombatService.TimerName, TimeSpan.FromMilliseconds(100), true), (timer.Name, timer.Interval, timer.Repeat));

        await _combat.StopAsync();

        Assert.Equal([timer.Id], _timers.Unregistered);
    }

    [Fact]
    public void Attack_PutsAPlayerInWarMode_AndTellsItWhomItFights()
    {
        Assert.True(_combat.Attack(_aria, _orc));

        Assert.Equal(_orc, _combat.TargetOf(_aria));
        Assert.Contains("war 2 True", _state.Flags);
        Assert.Equal(new Serial(Orc), Assert.IsType<CombatantPacket>(Assert.Single(_fixture.Sender.Sent)).Target);
    }

    [Fact]
    public void Attack_AnNpcFightsWithoutWarMode_AndTellsNoOne()
    {
        Assert.True(_combat.Attack(_orc, _aria));

        Assert.Equal(_aria, _combat.TargetOf(_orc));
        Assert.Empty(_state.Flags);
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void Attack_ANewTarget_ReplacesTheOldOne_TheSameOneIsKept()
    {
        var troll = Npc(0x101, new Point3D(0, 1, 0));
        _combat.Attack(_aria, _orc);
        _combat.Attack(_aria, _orc);

        Assert.Single(_fixture.Sender.Sent);

        Assert.True(_combat.Attack(_aria, troll));

        Assert.Equal(troll, _combat.TargetOf(_aria));
        Assert.Equal(
            [new Serial(Orc), new Serial(0x101)],
            _fixture.Sender.Sent.Select(packet => Assert.IsType<CombatantPacket>(packet).Target)
        );
    }

    [Fact]
    public void Attack_Itself_IsRefused()
    {
        Assert.False(_combat.Attack(_aria, _aria));

        Assert.Null(_combat.TargetOf(_aria));
    }

    [Fact]
    public void Attack_ATargetOnAnotherMap_OrNotInTheWorld_IsRefused()
    {
        var elsewhere = Npc(0x101, new Point3D(1, 0, 0), MapType.Felucca);
        var gone = new MobileEntity { Id = new Serial(0x102), Name = "gone", Map = MapType.Trammel, Location = new Point3D(1, 1, 0) };

        Assert.False(_combat.Attack(_aria, elsewhere));
        Assert.False(_combat.Attack(_aria, gone));
        Assert.Empty(_fixture.Sender.Sent);
    }

    [Fact]
    public void Attack_ATargetOutOfTheView_IsRefused()
    {
        var far = Npc(0x101, new Point3D(30, 0, 0));

        Assert.False(_combat.Attack(_aria, far));
    }

    [Fact]
    public void Attack_ATargetOutOfSight_IsRefused()
    {
        _sight.Allow = false;

        Assert.False(_combat.Attack(_aria, _orc));
        Assert.Empty(_state.Flags);
    }

    [Fact]
    public void Attack_AHiddenTarget_IsRefusedToAPlayer()
    {
        _orc.Hidden = true;

        Assert.False(_combat.Attack(_aria, _orc));
    }

    [Fact]
    public void Attack_AnInnocentThatDoesNotFightBack_MakesAPlayerACriminal()
    {
        _orc.Notoriety = NotorietyType.Innocent;

        _combat.Attack(_aria, _orc);

        Assert.Equal(["criminal 2"], _crimes.Calls);
    }

    [Theory]
    [InlineData(NotorietyType.Attackable)]
    [InlineData(NotorietyType.Enemy)]
    [InlineData(NotorietyType.Criminal)]
    [InlineData(NotorietyType.Murderer)]
    public void Attack_Anyone_ButAnInnocent_IsNoCrime(NotorietyType notoriety)
    {
        _orc.Notoriety = notoriety;

        _combat.Attack(_aria, _orc);

        Assert.Empty(_crimes.Calls);
    }

    [Fact]
    public void Attack_AnInnocentThatFightsThePlayer_IsNoCrime()
    {
        _orc.Notoriety = NotorietyType.Innocent;
        _combat.Attack(_orc, _aria);

        _combat.Attack(_aria, _orc);

        Assert.Empty(_crimes.Calls);
    }

    [Fact]
    public void Attack_AnNpcAttacker_IsNeverACriminal()
    {
        _aria.Notoriety = NotorietyType.Innocent;

        _combat.Attack(_orc, _aria);

        Assert.Empty(_crimes.Calls);
    }

    [Fact]
    public void FirstSwing_IsAtOnce_ThenOneEveryDelay()
    {
        _combat.Attack(_aria, _orc);
        _fixture.Sender.Sent.Clear();

        Tick();
        Assert.Equal(1, Swings());

        // 15000 / ((100 + 100) * 30) = 2.5 seconds.
        _clock.Advance(TimeSpan.FromSeconds(2.4));
        Tick();
        Assert.Equal(1, Swings());

        _clock.Advance(TimeSpan.FromSeconds(0.1));
        Tick();
        Assert.Equal(2, Swings());
    }

    [Fact]
    public void ASwing_TellsItsPlayer_AndPlaysTheFistsAnimation()
    {
        _combat.Attack(_aria, _orc);
        _fixture.Sender.Sent.Clear();

        Tick();

        var swing = _fixture.Sender.Sent.OfType<SwingPacket>().Single();
        Assert.Equal((new Serial(2), new Serial(Orc)), (swing.Attacker, swing.Defender));
        // Punch, 7 frames.
        Assert.Contains("Animated 2 31 7 1", _view.Calls);
    }

    [Fact]
    public void AMonster_SwingsItsAttack_AnAnimalItsOwn()
    {
        _combat.Attack(_orc, _aria);

        Tick();

        // Attack1 of a monster, 5 frames.
        Assert.Contains("Animated 256 4 5 1", _view.Calls);
    }

    [Fact]
    public void AHit_TakesTheDamageOffTheTarget_ShowsItAndHurtsIt()
    {
        // fists 1..8: the integer 4 is 5; tactics 50 leaves it as it is
        _random.Integers(4);
        _combat.Attack(_aria, _orc);
        _fixture.Sender.Sent.Clear();

        Tick();

        Assert.Equal(25, _orc.Hits);
        var shown = _fixture.Sender.Sent.OfType<DamagePacket>().Single();
        Assert.Equal((new Serial(Orc), 5), (shown.Target, shown.Damage));
        Assert.Contains((_aria, CombatService.FistsHitSound), _speech.Sounds);
        Assert.Contains((_orc, OrcHurt), _speech.Sounds);
        // GetHit of a monster, 4 frames.
        Assert.Contains("Animated 256 10 4 1", _view.Calls);
    }

    [Fact]
    public void TheHitRoll_IsTheWrestlingOfBoth_AndTeachesTheWrestlingOfAPlayer()
    {
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Wrestling, Base = 700 });
        _orc.Skills.Add(new MobileSkill { Skill = SkillType.Wrestling, Base = 300 });
        _combat.Attack(_aria, _orc);

        Tick();

        // (70 + 50) / ((30 + 50) * 2) = 0.75
        var (mobile, skill, chance) = Assert.Single(_skills.Chances);
        Assert.Equal((_aria, SkillType.Wrestling), (mobile, skill));
        Assert.Equal(0.75, chance, 6);
    }

    [Fact]
    public void AHit_TriesTacticsAndAnatomyOfTheAttacker_ToTeachThem()
    {
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.Contains((_aria, SkillType.Tactics, 0.0, 100.0), _skills.Checks);
        Assert.Contains((_aria, SkillType.Anatomy, 0.0, 100.0), _skills.Checks);
    }

    [Fact]
    public void AMiss_DoesNoDamage_PlaysTheMissSound_AndStillHasTheNpcFightBack()
    {
        _skills.ChanceResult = false;
        _combat.Attack(_aria, _orc);
        _fixture.Sender.Sent.Clear();

        Tick();

        Assert.Equal(30, _orc.Hits);
        Assert.Contains((_aria, CombatService.MissSound), _speech.Sounds);
        Assert.Empty(_fixture.Sender.Sent.OfType<DamagePacket>());
        Assert.Equal(_aria, _combat.TargetOf(_orc));
    }

    [Fact]
    public void ADamagedNpc_FightsBackTheOneWhoHitIt_AndSwingsAtOnce()
    {
        _random.Integers(4);
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.Equal(_aria, _combat.TargetOf(_orc));
        // The orc's dice give 8; its tactics 50 leaves it; halved on a player: 4. Its swing is on the same tick or the next.
        Tick();
        Assert.Equal(26, _aria.Hits);
        Assert.Contains((_orc, OrcAttack), _speech.Sounds);
    }

    [Fact]
    public void ADamagedNpc_AlreadyFightingAnother_KeepsIt()
    {
        var troll = Npc(0x101, new Point3D(0, 1, 0));
        _combat.Attack(_orc, troll);

        _combat.Attack(_aria, _orc);
        Tick();

        Assert.Equal(troll, _combat.TargetOf(_orc));
    }

    [Fact]
    public void ADamagedPlayer_DoesNotFightBackByItself()
    {
        _combat.Attack(_orc, _aria);

        Tick();

        Assert.Null(_combat.TargetOf(_aria));
    }

    [Fact]
    public void AnNpcAtNoHitPoints_Dies_ByTheKiller_AndTheFightIsOver()
    {
        _random.Integers(4);
        _orc.Hits = 5;
        _combat.Attack(_aria, _orc);
        _fixture.Sender.Sent.Clear();

        Tick();

        Assert.Equal([(_orc, (MobileEntity?)_aria)], _death.Killed);
        Assert.Equal(0, _orc.Hits);
        Assert.Null(_combat.TargetOf(_aria));
        Assert.Equal(Serial.Zero, _fixture.Sender.Sent.OfType<CombatantPacket>().Last().Target);
    }

    [Fact]
    public void AnNpcWithMoreHitPointsThanTheDamage_Lives()
    {
        _random.Integers(4);
        _orc.Hits = 6;
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.Empty(_death.Killed);
        Assert.Equal(1, _orc.Hits);
    }

    [Fact]
    public void APlayer_IsLeftWithOneHitPoint_NotKilled()
    {
        _aria.Hits = 2;
        _combat.Attack(_orc, _aria);

        Tick();

        Assert.Equal(1, _aria.Hits);
        Assert.Empty(_death.Killed);
    }

    [Fact]
    public void ANpcHittingAPlayer_DoesHalf_AndTheRateOfTheConfigDividesIt()
    {
        _config.NpcDamageRate = 2.0;
        _combat.Attack(_orc, _aria);

        Tick();

        // 8, halved 4, divided by 2
        Assert.Equal(28, _aria.Hits);
    }

    [Fact]
    public void TheArmorOfTheTargetAbsorbsAPartOfTheDamage()
    {
        _orc.Skills.Clear();
        _orc.Skills.Add(new MobileSkill { Skill = SkillType.Tactics, Base = 500 });
        _aria.Armor = 100;
        // the zone roll hits the chest (0.35 of 100 = 35), the second roll gives the least: 17
        _random.Doubles(0.99, 0.0);
        _combat.Attack(_orc, _aria);

        Tick();

        // 8 halved is 4: 17 absorbed leaves the least, 1
        Assert.Equal(29, _aria.Hits);
    }

    [Fact]
    public void TheStrengthTacticsAndAnatomyOfTheAttackerRaiseTheDamage()
    {
        _aria.Strength = 100;
        _aria.Skills.Single(skill => skill.Skill == SkillType.Tactics).Base = 1000;
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Anatomy, Base = 1000 });
        // 10: tactics 100 +50% = 15; mods strength .2 + anatomy .2 + .1 = .5: 22
        _random.Integers(9);
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.Equal(30 - 22, _orc.Hits);
    }

    [Fact]
    public void GlobalAttackSpeed_DividesTheDelayBetweenSwings()
    {
        _config.GlobalAttackSpeed = 2.0;
        _combat.Attack(_aria, _orc);
        Tick();

        _clock.Advance(TimeSpan.FromSeconds(1.2));
        Tick();
        Assert.Equal(1, Swings());

        _clock.Advance(TimeSpan.FromSeconds(0.1));
        Tick();
        Assert.Equal(2, Swings());
    }

    [Fact]
    public void AttackStamina_IsPaidByAPlayerForEachSwing_NotByAnNpc()
    {
        _config.AttackStamina = 2;
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.Equal(98, _aria.Stamina);
        Assert.Equal(100, _orc.Stamina);
    }

    [Fact]
    public void ASwing_ShowsAPlayerWhoHid()
    {
        _aria.Hidden = true;
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.False(_aria.Hidden);
    }

    [Fact]
    public void ATargetOutOfReach_IsNotSwungAt_UntilItIs()
    {
        _combat.Attack(_aria, _orc);
        _orc.Location = new Point3D(5, 0, 0);
        _fixture.Sender.Sent.Clear();

        Tick();
        Assert.Equal(0, Swings());

        _orc.Location = new Point3D(1, 0, 0);
        Tick();
        Assert.Equal(1, Swings());
    }

    [Fact]
    public void ATargetTooHighOrLow_IsOutOfReach()
    {
        _combat.Attack(_aria, _orc);
        _orc.Location = new Point3D(1, 0, 16);
        _fixture.Sender.Sent.Clear();

        Tick();

        Assert.Equal(0, Swings());
    }

    [Fact]
    public void MaxRange_IsHowFarASwingReaches()
    {
        _config.MaxRange = 3;
        _combat.Attack(_aria, _orc);
        _orc.Location = new Point3D(3, 0, 0);
        _fixture.Sender.Sent.Clear();

        Tick();

        Assert.Equal(1, Swings());
    }

    [Fact]
    public void AFighterThatDoesNotSwing_GivesTheFightUp_AfterTheCombatantTime()
    {
        _combat.Attack(_aria, _orc);
        _orc.Location = new Point3D(5, 0, 0);

        _clock.Advance(TimeSpan.FromSeconds(59));
        Tick();
        Assert.NotNull(_combat.TargetOf(_aria));

        _clock.Advance(TimeSpan.FromSeconds(2));
        Tick();

        Assert.Null(_combat.TargetOf(_aria));
        Assert.Equal(Serial.Zero, _fixture.Sender.Sent.OfType<CombatantPacket>().Last().Target);
    }

    [Fact]
    public void AFighterThatSwings_KeepsTheFight()
    {
        _skills.ChanceResult = false;
        _combat.Attack(_aria, _orc);

        for (var second = 0; second < 90; second += 3)
        {
            _clock.Advance(TimeSpan.FromSeconds(3));
            Tick();
        }

        Assert.NotNull(_combat.TargetOf(_aria));
    }

    [Fact]
    public void WarModeOff_EndsThePlayersFight()
    {
        _combat.Attack(_aria, _orc);
        _aria.WarMode = false;

        Tick();

        Assert.Null(_combat.TargetOf(_aria));
    }

    [Fact]
    public void ATargetThatLeavesTheWorld_EndsTheFight()
    {
        _combat.Attack(_aria, _orc);
        _fixture.Mobiles.LeaveWorld(_orc.Id);

        Tick();

        Assert.Null(_combat.TargetOf(_aria));
    }

    [Fact]
    public void Stop_EndsTheFight_AndTellsThePlayer()
    {
        _combat.Attack(_aria, _orc);

        _combat.Stop(_aria);
        _combat.Stop(_aria);

        Assert.Null(_combat.TargetOf(_aria));
        Assert.Equal(2, _fixture.Sender.Sent.Count);
        Assert.Equal(Serial.Zero, Assert.IsType<CombatantPacket>(_fixture.Sender.Sent[1]).Target);
    }

    [Fact]
    public void DisplayDamageNumbers_Off_ShowsNothing()
    {
        _config.DisplayDamageNumbers = false;
        _combat.Attack(_aria, _orc);

        Tick();

        Assert.Empty(_fixture.Sender.Sent.OfType<DamagePacket>());
        Assert.True(_orc.Hits < 30);
    }

    [Fact]
    public void ATickThatFails_IsLoggedAndTheTimerKeepsGoing()
    {
        _combat.Attack(_aria, _orc);
        _death.Kills = false;
        _fixture.Mobiles.LeaveWorld(new Serial(2));

        Tick();
        Tick();

        Assert.Null(_combat.TargetOf(_aria));
    }

    private void Tick()
    {
        // The service registered its timer on start; the tests call what the timer would.
        _combat.StartAsync().GetAwaiter().GetResult();
        var timer = _timers.Timers[^1];
        _timers.Fire(timer.Id);
    }

    private int Swings()
    {
        return _fixture.Sender.Sent.OfType<SwingPacket>().Count();
    }

    private MobileEntity Npc(int id, Point3D location, MapType map = MapType.Trammel)
    {
        var npc = new MobileEntity
        {
            Id = new Serial((uint)id), Name = "npc", TemplateId = "orc", Body = OrcBody, Map = map, Location = location,
            Hits = 30, HitsMax = 30, Stamina = 100, Notoriety = NotorietyType.Enemy
        };
        _fixture.Mobiles.EnterWorld(npc);

        return npc;
    }
}
