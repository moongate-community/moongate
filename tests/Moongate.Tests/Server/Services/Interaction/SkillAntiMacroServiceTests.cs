using Moongate.Server.Data.Interaction;
using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class SkillAntiMacroServiceTests
{
    [Test]
    public void AllowGain_WhenNpcRepeatsSameContext_ShouldRemainAllowed()
    {
        var now = DateTime.UtcNow;
        var service = new SkillAntiMacroService(() => now);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000001,
            IsPlayer = false,
            Location = new(100, 100, 0)
        };
        var context = new SkillGainContext(mobile.Location, (Serial)0x00000002);

        for (var index = 0; index < 6; index++)
        {
            Assert.That(service.AllowGain(mobile, UOSkillName.Archery, context), Is.True);
        }
    }

    [Test]
    public void AllowGain_WhenPlayerRepeatsSameSkillTargetAndBucket_ShouldEventuallyBlock()
    {
        var now = DateTime.UtcNow;
        var service = new SkillAntiMacroService(() => now);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000003,
            IsPlayer = true,
            Location = new(100, 100, 0)
        };
        var context = new SkillGainContext(mobile.Location, (Serial)0x00000004);

        Assert.That(service.AllowGain(mobile, UOSkillName.Archery, context), Is.True);
        Assert.That(service.AllowGain(mobile, UOSkillName.Archery, context), Is.True);
        Assert.That(service.AllowGain(mobile, UOSkillName.Archery, context), Is.True);
        Assert.That(service.AllowGain(mobile, UOSkillName.Archery, context), Is.False);
    }

    [Test]
    public void AllowGain_WhenPlayerChangesBucket_ShouldResetAllowance()
    {
        var now = DateTime.UtcNow;
        var service = new SkillAntiMacroService(() => now);
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000005,
            IsPlayer = true,
            Location = new(100, 100, 0)
        };
        var repeatedContext = new SkillGainContext(mobile.Location, (Serial)0x00000006);

        _ = service.AllowGain(mobile, UOSkillName.Archery, repeatedContext);
        _ = service.AllowGain(mobile, UOSkillName.Archery, repeatedContext);
        _ = service.AllowGain(mobile, UOSkillName.Archery, repeatedContext);

        var movedContext = new SkillGainContext(new Point3D(108, 108, 0), (Serial)0x00000006);

        Assert.That(service.AllowGain(mobile, UOSkillName.Archery, movedContext), Is.True);
    }
}
