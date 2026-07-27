using Moongate.Persistence.Entities;
using Moongate.Server.Handlers;
using Moongate.Server.Services.Mobiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class SkillLockChangeHandlerTests
{
    [Fact]
    public void TryApplyLock_TrainedSkill_SetsTheLockAndKeepsTheValue()
    {
        var mobile = new MobileEntity { Skills = { [40] = new() { Value = 733 } } };

        Assert.True(SkillLockChangeHandler.TryApplyLock(mobile, 40, SkillLockType.Locked, Skills()));

        Assert.Equal(SkillLockType.Locked, mobile.Skills[40].Lock);
        Assert.Equal(733, mobile.Skills[40].Value); // untouched
    }

    [Fact]
    public void TryApplyLock_UnknownSkill_ChangesNothing()
    {
        var mobile = new MobileEntity();

        Assert.False(SkillLockChangeHandler.TryApplyLock(mobile, 200, SkillLockType.Locked, Skills()));
        Assert.Empty(mobile.Skills);
    }

    [Fact]
    public void TryApplyLock_UntrainedSkill_CreatesTheEntryAtValueZero()
    {
        var mobile = new MobileEntity();

        Assert.True(SkillLockChangeHandler.TryApplyLock(mobile, 40, SkillLockType.Down, Skills()));

        Assert.Equal(SkillLockType.Down, mobile.Skills[40].Lock);
        Assert.Equal(0, mobile.Skills[40].Value);
        Assert.Equal(1000, mobile.Skills[40].Cap); // default ceiling
    }

    private static SkillService Skills()
    {
        var skills = new SkillService();
        skills.Register(new() { Id = 40, Name = "Swordsmanship" });

        return skills;
    }
}
