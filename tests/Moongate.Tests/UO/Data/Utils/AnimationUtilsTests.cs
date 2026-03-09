using Moongate.UO.Data.Utils;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.UO.Data.Utils;

public sealed class AnimationUtilsTests
{
    [Test]
    public void IsValidClientAction3DAnimation_ShouldReturnTrue_ForKnownAction()
    {
        var result = AnimationUtils.IsValidClientAction3DAnimation(AnimationUtils.BowAction);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsValidClientAction3DAnimation_ShouldReturnFalse_ForUnknownAction()
    {
        var result = AnimationUtils.IsValidClientAction3DAnimation(999);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryReadClientAction3D_ShouldReadBigEndianInt32()
    {
        var payload = new byte[] { 0x00, 0x00, 0x00, 0x20 };

        var ok = AnimationUtils.TryReadClientAction3D(payload, out var action);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(action, Is.EqualTo(32));
            }
        );
    }

    [Test]
    public void TryResolveAnimation_ShouldResolveSwingForHumanOnFoot()
    {
        var ok = AnimationUtils.TryResolveAnimation(
            AnimationIntent.SwingPrimary,
            UOBodyType.Human,
            isMounted: false,
            out var animation
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(animation.Action, Is.EqualTo(9));
                Assert.That(animation.FrameCount, Is.EqualTo(7));
            }
        );
    }

    [Test]
    public void TryResolveAnimation_ShouldResolveSwingForHumanMounted()
    {
        var ok = AnimationUtils.TryResolveAnimation(
            AnimationIntent.SwingSecondary,
            UOBodyType.Human,
            isMounted: true,
            out var animation
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(animation.Action, Is.EqualTo(29));
                Assert.That(animation.FrameCount, Is.EqualTo(7));
            }
        );
    }

    [Test]
    public void TryResolveAnimation_ShouldResolveHurtForMonster()
    {
        var ok = AnimationUtils.TryResolveAnimation(
            AnimationIntent.Hurt,
            UOBodyType.Monster,
            isMounted: false,
            out var animation
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(animation.Action, Is.EqualTo(10));
                Assert.That(animation.FrameCount, Is.EqualTo(4));
            }
        );
    }

    [Test]
    public void TryResolveAnimation_ShouldRejectBowWhenMounted()
    {
        var ok = AnimationUtils.TryResolveAnimation(
            AnimationIntent.Bow,
            UOBodyType.Human,
            isMounted: true,
            out _
        );

        Assert.That(ok, Is.False);
    }
}
