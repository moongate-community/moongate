using Moongate.Server.Data.Magic;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Server.Data.Magic;

[TestFixture]
public sealed class SpellCastContextTests
{
    [Test]
    public void Constructor_SetsProperties()
    {
        var casterId = new Serial(1);
        var targetId = new Serial(2);
        var context = new SpellCastContext(casterId, 4, SpellStateType.Casting, "timer-1", SpellTargetData.Mobile(targetId));

        Assert.Multiple(() =>
        {
            Assert.That(context.CasterId, Is.EqualTo(casterId));
            Assert.That(context.SpellId, Is.EqualTo(4));
            Assert.That(context.State, Is.EqualTo(SpellStateType.Casting));
            Assert.That(context.TimerId, Is.EqualTo("timer-1"));
            Assert.That(context.Target.Kind, Is.EqualTo(SpellTargetKind.Mobile));
            Assert.That(context.Target.TargetId, Is.EqualTo(targetId));
        });
    }
}
