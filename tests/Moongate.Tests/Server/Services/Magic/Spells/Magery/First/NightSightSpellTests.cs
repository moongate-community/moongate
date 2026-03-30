using Moongate.Server.Services.Magic.Spells.Magery.First;

namespace Moongate.Tests.Server.Services.Magic.Spells.Magery.First;

[TestFixture]
public sealed class NightSightSpellTests
{
    [Test]
    public void ApplyEffect_WhenTargetIsNull_SetsMarkerOnCaster()
    {
        var spell = new NightSightSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true
        };

        spell.ApplyEffect(caster, null);

        Assert.That(caster.TryGetCustomBoolean("magic.night_sight", out var isMarked), Is.True);
        Assert.That(isMarked, Is.True);
    }

    [Test]
    public void ApplyEffect_WhenRecipientIsDead_DoesNotSetMarker()
    {
        var spell = new NightSightSpell();
        var caster = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = true
        };
        var target = new Moongate.UO.Data.Persistence.Entities.UOMobileEntity
        {
            IsAlive = false
        };

        spell.ApplyEffect(caster, target);

        Assert.That(target.TryGetCustomBoolean("magic.night_sight", out _), Is.False);
    }
}
