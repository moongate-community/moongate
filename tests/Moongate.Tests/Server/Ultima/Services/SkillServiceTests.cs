using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Skills;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Randomness;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SkillServiceTests
{
    private readonly RecordingMobileStateService _state = new();
    private readonly SkillsConfig _config = new();
    private readonly ScriptedRandom _random = new();
    private readonly MobileEntity _aria = new() { Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42) };
    private readonly MobileEntity _orc = new() { Id = new Serial(0x100), Name = "an orc" };
    private readonly SkillService _skills;

    public SkillServiceTests()
    {
        _skills = new(
            _state,
            new StubDataLoaderService().With(
                new SkillContent { Id = SkillType.Hiding, GainFactor = 1.0 },
                new SkillContent { Id = SkillType.Magery, GainFactor = 0.5 }
            ),
            _config,
            _random
        );
    }

    [Theory]
    [InlineData(300, 40.0, 100.0, false)]
    [InlineData(0, 0.1, 100.0, false)]
    [InlineData(1000, 0.0, 100.0, true)]
    [InlineData(500, 0.0, 50.0, true)]
    [InlineData(500, 60.0, 60.0, false)]
    [InlineData(500, 50.0, 50.0, true)]
    [InlineData(500, 50.0, 40.0, true)]
    public void Check_BelowTheMinimumFails_AtTheMaximumSucceeds_AndNeitherRollsNorTeaches(int tenths, double min, double max, bool expected)
    {
        Has(_aria, SkillType.Hiding, tenths);

        Assert.Equal(expected, _skills.Check(_aria, SkillType.Hiding, min, max));

        Assert.Equal(0, _random.Rolls);
        Assert.Empty(_state.SkillsSet);
    }

    [Theory]
    [InlineData(0.25, true)]
    [InlineData(0.2499, true)]
    [InlineData(0.2501, false)]
    public void Check_InBetween_SucceedsWithAChanceThatGrowsInALine(double roll, bool expected)
    {
        // 55 between 40 and 100: a quarter of the way.
        Has(_aria, SkillType.Hiding, 550);
        _random.Doubles(roll);

        Assert.Equal(expected, _skills.Check(_aria, SkillType.Hiding, 40, 100));
    }

    [Fact]
    public void Check_ASkillBelowTenPoints_AlwaysLearns_ByOneToFourTenths()
    {
        Has(_aria, SkillType.Hiding, 50);
        // A failure, and a gain of three tenths (the integer asked is 0 to 3, plus one).
        _random.Doubles(0.999).Integers(2);

        Assert.False(_skills.Check(_aria, SkillType.Hiding, 0, 100));

        Assert.Equal((_aria, SkillType.Hiding, 53, (int?)null), Assert.Single(_state.SkillsSet));
        // The roll of the check only: no roll says whether it learns.
        Assert.Equal(2, _random.Rolls);
    }

    [Theory]
    // 50 of 100 and 50 of 700 in all, the task half way: room (0.9286 + 0.5) / 2 = 0.7143; a success adds
    // 0.5 * 0.5 = 0.25, a failure 0.5 * 0.2 = 0.1; halved: 0.4821 and 0.4071.
    [InlineData(0.4, 0.48, true)]
    [InlineData(0.4, 0.49, false)]
    [InlineData(0.6, 0.40, true)]
    [InlineData(0.6, 0.41, false)]
    public void Check_LearnsMoreOftenWithRoomUnderTheCapsAndAHardTask_AndLessFromAFailure(double roll, double gainRoll, bool learns)
    {
        Has(_aria, SkillType.Hiding, 500);
        // The check, whether it learns, whether a skill gives way (none is set down).
        _random.Doubles(roll, gainRoll, 0.999);

        _skills.Check(_aria, SkillType.Hiding, 0, 100);

        Assert.Equal(learns ? [(_aria, SkillType.Hiding, 501, (int?)null)] : [], _state.SkillsSet);
    }

    [Fact]
    public void Check_ASkillThatGrowsSlowly_LearnsHalfAsOften()
    {
        // Magery has a gain factor of 0.5: the 0.4821 of a success becomes 0.2411.
        Has(_aria, SkillType.Magery, 500);
        _random.Doubles(0.4, 0.25);
        _skills.Check(_aria, SkillType.Magery, 0, 100);
        Assert.Empty(_state.SkillsSet);

        _random.Doubles(0.4, 0.24, 0.999);
        _skills.Check(_aria, SkillType.Magery, 0, 100);
        Assert.Single(_state.SkillsSet);
    }

    [Fact]
    public void Check_WithNoRoomLeft_StillLearnsOnceInAHundredTries()
    {
        // At both caps and an easy task: the chance would be next to nothing.
        Has(_aria, SkillType.Hiding, 990, cap: 990);
        Has(_aria, SkillType.Magery, 6010);
        _random.Doubles(0.0, 0.011);
        _skills.Check(_aria, SkillType.Hiding, 0, 100);
        Assert.Equal(2, _random.Rolls);

        // It may learn, but the skill is at its cap: nothing rises.
        _random.Doubles(0.0, 0.009);
        _skills.Check(_aria, SkillType.Hiding, 0, 100);
        Assert.Empty(_state.SkillsSet);
    }

    [Theory]
    [InlineData(SkillLockType.Locked)]
    [InlineData(SkillLockType.Down)]
    public void Check_ASkillThatIsNotSetToGoUp_NeverRises(SkillLockType kept)
    {
        Has(_aria, SkillType.Hiding, 500).Lock = kept;
        _random.Doubles(0.4, 0.0);

        _skills.Check(_aria, SkillType.Hiding, 0, 100);

        Assert.Empty(_state.SkillsSet);
    }

    [Fact]
    public void Check_ASkillNeverGoesAboveItsCap()
    {
        // Below ten points it gains up to four tenths: the cap stops it.
        Has(_aria, SkillType.Hiding, 98, cap: 100);
        _random.Doubles(0.999).Integers(3);

        _skills.Check(_aria, SkillType.Hiding, 0, 100);

        Assert.Equal(100, Assert.Single(_state.SkillsSet).Value);
    }

    [Fact]
    public void Check_AtTheCapOfAllSkills_RisesOnlyWhenASkillSetToGoDownGivesWay()
    {
        Has(_aria, SkillType.Hiding, 500);
        Has(_aria, SkillType.Magery, 6500);
        // 700 points in all: the try teaches (0.0), but nothing is set to go down.
        _random.Doubles(0.4, 0.0, 0.0);
        _skills.Check(_aria, SkillType.Hiding, 0, 100);
        Assert.Empty(_state.SkillsSet);

        Skill(_aria, SkillType.Magery).Lock = SkillLockType.Down;
        _random.Doubles(0.4, 0.0, 0.0);
        _skills.Check(_aria, SkillType.Hiding, 0, 100);

        Assert.Equal(
            [(_aria, SkillType.Magery, 6499, (int?)null), (_aria, SkillType.Hiding, 501, (int?)null)],
            _state.SkillsSet
        );
    }

    [Fact]
    public void Check_UnderTheCapOfAllSkills_ASkillSetToGoDownGivesWayOnlyAsOftenAsTheCharacterIsFull()
    {
        // 350 of 700 points: half the times.
        Has(_aria, SkillType.Hiding, 500);
        Has(_aria, SkillType.Magery, 3000).Lock = SkillLockType.Down;
        _random.Doubles(0.4, 0.0, 0.51);

        _skills.Check(_aria, SkillType.Hiding, 0, 100);

        Assert.Equal([(_aria, SkillType.Hiding, 501, (int?)null)], _state.SkillsSet);
    }

    [Fact]
    public void Check_AnNpc_SucceedsOrFailsAsAnyone_ButNeverLearns()
    {
        Has(_orc, SkillType.Hiding, 50);
        _random.Doubles(0.01);

        Assert.True(_skills.Check(_orc, SkillType.Hiding, 0, 100));

        Assert.Empty(_state.SkillsSet);
        Assert.Equal(1, _random.Rolls);
    }

    [Fact]
    public void Check_WithGainTurnedOff_NobodyLearns()
    {
        _config.GainEnabled = false;
        Has(_aria, SkillType.Hiding, 50);

        _skills.Check(_aria, SkillType.Hiding, 0, 100);

        Assert.Empty(_state.SkillsSet);
    }

    [Fact]
    public void Check_ASkillNeverTrained_IsAtZero()
    {
        _random.Doubles(0.5).Integers(0);

        Assert.False(_skills.Check(_aria, SkillType.Hiding, 0, 100));

        // It learns all the same: one tenth.
        Assert.Equal((_aria, SkillType.Hiding, 1, (int?)null), Assert.Single(_state.SkillsSet));
    }

    [Fact]
    public void Check_ASkillThatDoesNotExist_Fails()
    {
        Assert.False(_skills.Check(_aria, (SkillType)200, 0, 100));
    }

    [Fact]
    public void Total_IsTheSumOfTheSkillsInTenths()
    {
        Has(_aria, SkillType.Hiding, 500);
        Has(_aria, SkillType.Magery, 255);

        Assert.Equal(755, _skills.Total(_aria));
    }

    private static MobileSkill Has(MobileEntity mobile, SkillType skill, int tenths, int cap = 1000)
    {
        var known = new MobileSkill { Skill = skill, Base = tenths, Cap = cap };
        mobile.Skills.Add(known);

        return known;
    }

    private static MobileSkill Skill(MobileEntity mobile, SkillType skill)
    {
        return mobile.Skills.Single(known => known.Skill == skill);
    }
}
