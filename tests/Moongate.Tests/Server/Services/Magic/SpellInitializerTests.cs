using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Magic.Spells.Magery.First;
using Moongate.Server.Services.Magic.Spells.Magery.Fourth;
using Moongate.Server.Services.Magic.Spells.Magery.Second;
using Moongate.Server.Services.Magic.Spells.Magery.Third;
using Moongate.Server.Types.Magic;

namespace Moongate.Tests.Server.Services.Magic;

[TestFixture]
public sealed class SpellInitializerTests
{
    [Test]
    public void RegisterAll_RegistersImplementedMagerySpells()
    {
        var registry = new SpellRegistry();

        SpellInitializer.RegisterAll(registry);

        Assert.Multiple(() =>
        {
            Assert.That(registry.Get(SpellIds.Magery.First.Clumsy), Is.TypeOf<ClumsySpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.Feeblemind), Is.TypeOf<FeeblemindSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.Heal), Is.TypeOf<HealSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.MagicArrow), Is.TypeOf<MagicArrowSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.NightSight), Is.TypeOf<NightSightSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.ReactiveArmor), Is.TypeOf<ReactiveArmorSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.Weaken), Is.TypeOf<WeakenSpell>());
            Assert.That(registry.Get(SpellIds.Magery.Second.Agility), Is.TypeOf<AgilitySpell>());
            Assert.That(registry.Get(SpellIds.Magery.Third.Fireball), Is.TypeOf<FireballSpell>());
            Assert.That(registry.Get(SpellIds.Magery.Fourth.GreaterHeal), Is.TypeOf<GreaterHealSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.CreateFood), Is.Null);
        });
    }
}
