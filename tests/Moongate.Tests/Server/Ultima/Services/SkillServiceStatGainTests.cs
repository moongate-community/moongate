using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

/// <summary>
///     The stats a successful skill check can raise, as ModernUO's classic rule: each stat the skill favours, one
///     point at a time, once in ten minutes, to 100 each and 225 in all.
/// </summary>
/// <remarks>
///     Every check rolls, in order: the success, whether the skill learns (it is above ten points), then for each
///     stat the skill favours its chance and, when that passes, whether the character gives way (atrophy). Rolls a
///     test does not queue are 0.999: a chance that does not pass.
/// </remarks>
public sealed class SkillServiceStatGainTests
{
    private const double Success = 0.3;
    private const double NoSkillGain = 0.999;
    private const double StatPasses = 0.1;
    private const double StatFails = 0.999;
    private const double NoAtrophy = 0.9;
    private const double Atrophy = 0.5;

    private readonly RecordingMobileStateService _state = new();
    private readonly SkillsConfig _config = new();
    private readonly ScriptedRandom _random = new();
    private readonly SettableClock _clock = new();
    // 15 / 33.3 is 0.45 for strength, 0.3 for dexterity and 0.15 for intelligence.
    private readonly SkillContent _anatomy = new()
    {
        Id = SkillType.Anatomy, GainFactor = 1.0, StrGain = 15, DexGain = 10, IntGain = 5
    };
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42),
        Strength = 50, Dexterity = 50, Intelligence = 50, HitsMax = 50, StaminaMax = 50, ManaMax = 50
    };
    private readonly SkillService _skills;

    public SkillServiceStatGainTests()
    {
        _skills = new(_state, new StubDataLoaderService().With(_anatomy), _config, _random, _clock);
        // Fifty points: a success half the time.
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Anatomy, Base = 500 });
    }

    [Fact]
    public void ASuccessfulCheck_MayRaiseAStatTheSkillFavours_ByOne_AndTheMaximumOfItsBarWithIt()
    {
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);

        Assert.True(Check());

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((51, 50, 50), (change.Strength, change.Dexterity, change.Intelligence));
        Assert.Equal((51, 50, 50), (change.HitsMax, change.StaminaMax, change.ManaMax));
    }

    [Fact]
    public void Dexterity_RaisesTheStaminaMaximum()
    {
        _random.Doubles(Success, NoSkillGain, StatFails, StatPasses, NoAtrophy);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((50, 51, 50), (change.Strength, change.Dexterity, change.Intelligence));
        Assert.Equal((50, 51, 50), (change.HitsMax, change.StaminaMax, change.ManaMax));
    }

    [Fact]
    public void Intelligence_RaisesTheManaMaximum()
    {
        // 5 / 33.3 is 0.15: a roll of 0.1 passes it.
        _random.Doubles(Success, NoSkillGain, StatFails, StatFails, StatPasses, NoAtrophy);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((50, 50, 51), (change.Strength, change.Dexterity, change.Intelligence));
        Assert.Equal((50, 50, 51), (change.HitsMax, change.StaminaMax, change.ManaMax));
    }

    [Fact]
    public void AFailedCheck_RaisesNoStat_AndRollsNoChanceForOne()
    {
        _random.Doubles(0.6, NoSkillGain);

        Assert.False(Check());

        Assert.Empty(_state.Stats);
        // The success and whether the skill learns, nothing else.
        Assert.Equal(2, _random.Rolls);
    }

    [Fact]
    public void AStatWhoseLockIsNotUp_IsNotTried()
    {
        _aria.StrLock = StatLockType.Locked;
        _aria.DexLock = StatLockType.Down;
        _aria.IntLock = StatLockType.Locked;
        _random.Doubles(Success, NoSkillGain);
        _random.Rest = 0.0;

        Assert.True(Check());

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void ASkillThatFavoursNoStat_RaisesNone()
    {
        _anatomy.StrGain = _anatomy.DexGain = _anatomy.IntGain = 0;
        _random.Doubles(Success, NoSkillGain);
        _random.Rest = 0.0;

        Assert.True(Check());

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void AStat_IsTriedOnceInTenMinutes_AndTheOthersAreIndependent()
    {
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);
        Check();
        Assert.Equal(51, Assert.Single(_state.Stats).Change.Strength);

        // Not ten minutes yet: strength is not tried again, and dexterity, which was not, is.
        _clock.Advance(TimeSpan.FromMinutes(9.9));
        _random.Doubles(Success, NoSkillGain, StatPasses, StatPasses, NoAtrophy);
        Check();
        Assert.Equal(2, _state.Stats.Count);
        Assert.Equal((50, 51), (_state.Stats[1].Change.Strength, _state.Stats[1].Change.Dexterity));

        // Ten minutes after the first try, strength can be tried again.
        _clock.Advance(TimeSpan.FromMinutes(0.1));
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);
        Check();
        Assert.Equal(3, _state.Stats.Count);
        Assert.Equal(51, _state.Stats[2].Change.Strength);
    }

    [Fact]
    public void TheWaitOutlivesALogout_TheCharacterIsRebuiltFromTheDatabaseWithTheSameSerial()
    {
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);
        Check();
        Assert.Single(_state.Stats);

        // Out and in again a minute later: a new entity of the same character.
        _clock.Advance(TimeSpan.FromMinutes(1));
        var again = new MobileEntity
        {
            Id = _aria.Id, Name = "Aria", AccountId = _aria.AccountId, Strength = 51, Dexterity = 50, Intelligence = 50,
            HitsMax = 51, StaminaMax = 50, ManaMax = 50
        };
        again.Skills.Add(new MobileSkill { Skill = SkillType.Anatomy, Base = 500 });
        _random.Doubles(Success, NoSkillGain, StatPasses, StatFails, StatFails);

        _skills.Check(again, SkillType.Anatomy, 0, 100);

        Assert.Single(_state.Stats);
    }

    [Fact]
    public void WhenTheStateServiceRefusesTheChange_NothingIsRaised_AndTheWaitStays()
    {
        _state.Result = false;
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);

        Check();

        // Refused, so unchanged: the stub records the ask and does not write.
        Assert.Equal(50, _aria.Strength);
    }

    [Theory]
    [InlineData(StatType.Dex, 60, 40, 40 - 1)]
    [InlineData(StatType.Dex, 40, 60, 40 - 1)]
    [InlineData(StatType.Int, 60, 40, 40 - 1)]
    [InlineData(StatType.Int, 40, 60, 40 - 1)]
    public void WhicheverStatRises_TheLowerOfTheOtherTwoGivesWay(StatType rises, int firstOther, int secondOther, int lowered)
    {
        // The two others, in the order strength, dexterity, intelligence; both locked down, the total at the cap.
        var others = Enum.GetValues<StatType>().Where(stat => stat != rises).ToArray();
        _aria.Strength = _aria.Dexterity = _aria.Intelligence = 50;
        Set(others[0], firstOther);
        Set(others[1], secondOther);
        _aria.StrLock = rises == StatType.Str ? StatLockType.Up : StatLockType.Down;
        _aria.DexLock = rises == StatType.Dex ? StatLockType.Up : StatLockType.Down;
        _aria.IntLock = rises == StatType.Int ? StatLockType.Up : StatLockType.Down;
        _config.StatCap = _aria.Strength + _aria.Dexterity + _aria.Intelligence;
        // Only the stat that is up is rolled for: the others are locked down.
        _random.Doubles(Success, NoSkillGain, StatPasses);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        var values = new[] { change.Strength!.Value, change.Dexterity!.Value, change.Intelligence!.Value };
        Assert.Equal(51, values[(int)rises]);
        Assert.Equal(lowered, values.Where((_, index) => index != (int)rises).Min());
        Assert.Equal(_config.StatCap, values.Sum());
    }

    [Fact]
    public void ADownStat_KeepsGivingWayWhenTheTotalIsOverALoweredCap()
    {
        _aria.DexLock = StatLockType.Down;
        _config.StatCap = 100;
        _random.Doubles(Success, NoSkillGain, StatPasses);

        Check();

        // Over the cap nothing rises, but the stat locked down still gives its point.
        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((50, 49, 50), (change.Strength, change.Dexterity, change.Intelligence));
    }

    [Fact]
    public void TheWaitStartsWhenTheStatIsTried_EvenWhenNothingRises()
    {
        // At the cap with nothing to give way: strength is tried and cannot rise.
        _config.StatCap = 150;
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);
        Check();
        Assert.Empty(_state.Stats);

        // Five minutes later, room made: strength still waits, dexterity is tried and rises.
        _config.StatCap = 225;
        _clock.Advance(TimeSpan.FromMinutes(5));
        _random.Doubles(Success, NoSkillGain, StatPasses, StatPasses, NoAtrophy);
        Check();

        Assert.Equal((50, 51), (Assert.Single(_state.Stats).Change.Strength, _state.Stats[0].Change.Dexterity));
    }

    [Fact]
    public void AtTheCap_AStatRisesOnlyAsAnotherLockedDownGivesWay()
    {
        _config.StatCap = 150;
        _aria.DexLock = StatLockType.Down;
        _random.Doubles(Success, NoSkillGain, StatPasses);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((51, 49, 50), (change.Strength, change.Dexterity, change.Intelligence));
        Assert.Equal((51, 49, 50), (change.HitsMax, change.StaminaMax, change.ManaMax));
    }

    [Fact]
    public void AtTheCap_WithNothingLockedDown_NoStatRises()
    {
        _config.StatCap = 150;
        _random.Doubles(Success, NoSkillGain, StatPasses);

        Check();

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void BelowTheCap_TheNearerTheTotal_TheMoreOftenAStatGivesWay()
    {
        // 150 of 225 is 0.667: a roll of 0.5 makes it atrophy, one of 0.9 does not.
        _aria.DexLock = StatLockType.Down;
        _random.Doubles(Success, NoSkillGain, StatPasses, Atrophy);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((51, 49), (change.Strength, change.Dexterity));
    }

    [Fact]
    public void BelowTheCap_WithoutAtrophy_NothingGivesWay()
    {
        _aria.DexLock = StatLockType.Down;
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((51, 50), (change.Strength, change.Dexterity));
    }

    [Theory]
    [InlineData(40, 50, 40 - 1, 50)]
    [InlineData(60, 50, 60, 50 - 1)]
    [InlineData(40, 40, 40, 40 - 1)]
    public void WhenBothOthersCanGiveWay_TheLowerOneDoes_TheSecondWhenTheyAreEqual(int dex, int intel, int expectedDex, int expectedInt)
    {
        _aria.Dexterity = dex;
        _aria.Intelligence = intel;
        _aria.DexLock = StatLockType.Down;
        _aria.IntLock = StatLockType.Down;
        _config.StatCap = 50 + dex + intel;
        _random.Doubles(Success, NoSkillGain, StatPasses);

        Check();

        var change = Assert.Single(_state.Stats).Change;
        Assert.Equal((51, expectedDex, expectedInt), (change.Strength, change.Dexterity, change.Intelligence));
    }

    [Fact]
    public void AStatAtTenCannotGiveWay()
    {
        _aria.Dexterity = 10;
        _aria.DexLock = StatLockType.Down;
        _config.StatCap = 110;
        _random.Doubles(Success, NoSkillGain, StatPasses);

        Check();

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void AStatAtTheMaximum_DoesNotRise()
    {
        _aria.Strength = 100;
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);

        Check();

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void TheMaximumIsTheOneOfTheConfig()
    {
        _config.StatMax = 50;
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);

        Check();

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void ANpc_NeverGainsAStat()
    {
        var orc = new MobileEntity { Id = new Serial(0x100), Name = "an orc", Strength = 50, Dexterity = 50, Intelligence = 50 };
        orc.Skills.Add(new MobileSkill { Skill = SkillType.Anatomy, Base = 500 });
        _random.Doubles(Success);
        _random.Rest = 0.0;

        Assert.True(_skills.Check(orc, SkillType.Anatomy, 0, 100));

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void WithGainOff_NoStatRises()
    {
        _config.GainEnabled = false;
        _random.Doubles(Success);
        _random.Rest = 0.0;

        Assert.True(Check());

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void ASkillWithoutAnEntryInTheData_RaisesNoStat()
    {
        _aria.Skills.Add(new MobileSkill { Skill = SkillType.Magery, Base = 500 });
        _random.Doubles(Success);
        _random.Rest = 0.0;

        Assert.True(_skills.Check(_aria, SkillType.Magery, 0, 100));

        Assert.Empty(_state.Stats);
    }

    [Fact]
    public void WithNoWaitBetweenGains_AStatIsTriedAtEverySuccess()
    {
        _config.StatGainMinutes = 0;
        _state.Apply = true;
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy, StatFails, StatFails);
        _random.Doubles(Success, NoSkillGain, StatPasses, NoAtrophy);

        Check();
        Check();

        Assert.Equal([51, 52], _state.Stats.Select(stat => stat.Change.Strength));
    }

    private void Set(StatType stat, int value)
    {
        switch (stat)
        {
            case StatType.Str:
                _aria.Strength = value;

                break;
            case StatType.Dex:
                _aria.Dexterity = value;

                break;
            default:
                _aria.Intelligence = value;

                break;
        }
    }

    private bool Check()
    {
        return _skills.Check(_aria, SkillType.Anatomy, 0, 100);
    }
}
