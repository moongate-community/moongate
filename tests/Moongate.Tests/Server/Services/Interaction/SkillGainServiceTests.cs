using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class SkillGainServiceTests
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
            ),
            new(
                (int)UOSkillName.Tactics,
                "Tactics",
                0,
                100,
                0,
                "Tactician",
                0,
                0,
                0,
                1,
                "Tactics",
                Stat.Dexterity,
                Stat.Strength
            )
        ];

    [Test]
    public void TryGain_WhenSkillLockIsNotUp_ShouldNotChangeSkill()
    {
        var mobile = CreateMobile();
        mobile.SetSkill(UOSkillName.Archery, 500, lockState: UOSkillLock.Locked);
        var service = new SkillGainService(() => 0.0);

        var result = service.TryGain(mobile, UOSkillName.Archery, 0.25, true);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.SkillIncreased, Is.False);
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(500));
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Value, Is.EqualTo(500));
            }
        );
    }

    [Test]
    public void TryGain_WhenGainSucceeds_ShouldIncreaseBaseAndValueByOne()
    {
        var mobile = CreateMobile();
        mobile.SetSkill(UOSkillName.Archery, 100);
        var service = new SkillGainService(() => 0.0);

        var result = service.TryGain(mobile, UOSkillName.Archery, 0.0, true);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.SkillIncreased, Is.True);
                Assert.That(result.SkillName, Is.EqualTo(UOSkillName.Archery));
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(101));
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Value, Is.EqualTo(101));
            }
        );
    }

    [Test]
    public void TryGain_WhenTotalSkillCapWouldOverflow_ShouldLowerFirstDownLockedSkill()
    {
        var mobile = CreateMobile();
        mobile.SetSkill(UOSkillName.Archery, 1000, cap: 1100);
        mobile.SetSkill(UOSkillName.Anatomy, 1000, lockState: UOSkillLock.Down);
        mobile.SetSkill(UOSkillName.Tactics, 5000, lockState: UOSkillLock.Locked);
        var service = new SkillGainService(() => 0.0);

        var result = service.TryGain(mobile, UOSkillName.Archery, 0.0, true);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.SkillIncreased, Is.True);
                Assert.That(result.LoweredSkillName, Is.EqualTo(UOSkillName.Anatomy));
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(1001));
                Assert.That(mobile.GetSkill(UOSkillName.Anatomy)!.Base, Is.EqualTo(999));
                Assert.That(mobile.GetTotalSkillBaseFixedPoint(), Is.EqualTo(7000));
            }
        );
    }

    [Test]
    public void TryGain_WhenTotalSkillCapWouldOverflowAndNoDownSkillExists_ShouldNotGain()
    {
        var mobile = CreateMobile();
        mobile.SetSkill(UOSkillName.Archery, 1000);
        mobile.SetSkill(UOSkillName.Anatomy, 1000, lockState: UOSkillLock.Locked);
        mobile.SetSkill(UOSkillName.Tactics, 5000, lockState: UOSkillLock.Locked);
        var service = new SkillGainService(() => 0.0);

        var result = service.TryGain(mobile, UOSkillName.Archery, 0.0, true);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.SkillIncreased, Is.False);
                Assert.That(result.LoweredSkillName, Is.Null);
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(1000));
                Assert.That(mobile.GetTotalSkillBaseFixedPoint(), Is.EqualTo(7000));
            }
        );
    }

    private static UOMobileEntity CreateMobile()
    {
        var mobile = new UOMobileEntity();
        mobile.InitializeSkills();

        return mobile;
    }
}
