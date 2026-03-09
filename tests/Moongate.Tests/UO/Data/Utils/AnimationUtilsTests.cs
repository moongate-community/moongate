using Moongate.UO.Data.Utils;

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
}
