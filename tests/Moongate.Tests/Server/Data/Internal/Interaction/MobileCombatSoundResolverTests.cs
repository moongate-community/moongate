using Moongate.Server.Data.Internal.Interaction;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Data.Internal.Interaction;

public sealed class MobileCombatSoundResolverTests
{
    [Test]
    public void TryResolve_ShouldPreferRuntimeMobileSounds()
    {
        var resolver = new MobileCombatSoundResolver();
        var mobile = new UOMobileEntity();
        mobile.Sounds[MobileSoundType.Attack] = 0x0444;

        var resolved = resolver.TryResolve(mobile, MobileSoundType.Attack, out var soundId);

        Assert.Multiple(
            () =>
            {
                Assert.That(resolved, Is.True);
                Assert.That(soundId, Is.EqualTo(0x0444));
            }
        );
    }

    [Test]
    public void TryResolve_ShouldUseFallbackWhenRuntimeSoundMissing()
    {
        var resolver = new MobileCombatSoundResolver();
        var mobile = new UOMobileEntity();

        var resolved = resolver.TryResolve(mobile, MobileSoundType.Attack, out var soundId);

        Assert.Multiple(
            () =>
            {
                Assert.That(resolved, Is.True);
                Assert.That(soundId, Is.GreaterThan(0));
            }
        );
    }

    [Test]
    public void TryResolve_ShouldReturnFalseWhenNoRuntimeOrFallbackExists()
    {
        var resolver = new MobileCombatSoundResolver();
        var mobile = new UOMobileEntity();

        var resolved = resolver.TryResolve(mobile, MobileSoundType.Die, out _);

        Assert.That(resolved, Is.False);
    }
}
