using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class StatGainServiceTests
{
    [SetUp]
    public void SetUp()
        => SkillInfo.Table =
        [
            new(
                (int)UOSkillName.Archery,
                "Archery",
                0,
                100,
                0,
                "Archer",
                0,
                0,
                0,
                1,
                "Archery",
                Stat.Dexterity,
                Stat.Strength
            ),
            new(
                (int)UOSkillName.Anatomy,
                "Anatomy",
                100,
                0,
                0,
                "Anatomist",
                0,
                0,
                0,
                1,
                "Anatomy",
                Stat.Strength,
                Stat.Intelligence
            )
        ];

    [Test]
    public void TryApply_WhenPrimaryStatRollWins_ShouldIncreasePrimaryStat()
    {
        var mobile = CreateMobile();
        var service = new StatGainService(() => 0.0, () => 0.0);

        var result = service.TryApply(mobile, UOSkillName.Archery);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.StatIncreased, Is.True);
                Assert.That(result.GainedStat, Is.EqualTo(Stat.Dexterity));
                Assert.That(mobile.Dexterity, Is.EqualTo(51));
            }
        );
    }

    [Test]
    public void TryApply_WhenPrimaryStatLocked_ShouldFallbackToSecondaryIfAvailable()
    {
        var mobile = CreateMobile();
        mobile.DexterityLock = UOSkillLock.Locked;
        var service = new StatGainService(() => 0.0, () => 0.0);

        var result = service.TryApply(mobile, UOSkillName.Archery);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.StatIncreased, Is.True);
                Assert.That(result.GainedStat, Is.EqualTo(Stat.Strength));
                Assert.That(mobile.Strength, Is.EqualTo(51));
            }
        );
    }

    [Test]
    public void TryApply_WhenStatCapIsFull_ShouldLowerDownLockedOtherStat()
    {
        var mobile = CreateMobile();
        mobile.Strength = 100;
        mobile.Dexterity = 100;
        mobile.Intelligence = 25;
        mobile.StatCap = 225;
        mobile.StrengthLock = UOSkillLock.Down;
        var service = new StatGainService(() => 0.0, () => 0.0);

        var result = service.TryApply(mobile, UOSkillName.Archery);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.StatIncreased, Is.True);
                Assert.That(result.GainedStat, Is.EqualTo(Stat.Dexterity));
                Assert.That(result.LoweredStat, Is.EqualTo(Stat.Strength));
                Assert.That(mobile.Strength, Is.EqualTo(99));
                Assert.That(mobile.Dexterity, Is.EqualTo(101));
                Assert.That(mobile.GetTotalBaseStats(), Is.EqualTo(225));
            }
        );
    }

    [Test]
    public void TryApply_WhenStatCapIsFullAndNoDownLockedStatExists_ShouldNotIncrease()
    {
        var mobile = CreateMobile();
        mobile.Strength = 100;
        mobile.Dexterity = 100;
        mobile.Intelligence = 25;
        mobile.StatCap = 225;
        mobile.StrengthLock = UOSkillLock.Locked;
        mobile.IntelligenceLock = UOSkillLock.Locked;
        var service = new StatGainService(() => 0.0, () => 0.0);

        var result = service.TryApply(mobile, UOSkillName.Archery);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.StatIncreased, Is.False);
                Assert.That(mobile.Dexterity, Is.EqualTo(100));
                Assert.That(mobile.GetTotalBaseStats(), Is.EqualTo(225));
            }
        );
    }

    private static UOMobileEntity CreateMobile()
        => new()
        {
            Strength = 50,
            Dexterity = 50,
            Intelligence = 25
        };
}
