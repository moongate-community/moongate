using Moongate.Server.Services.Magic.Spells.Magery.First;
using Moongate.Server.Services.Magic.Spells.Magery.Fifth;
using Moongate.Server.Services.Magic.Spells.Magery.Fourth;
using Moongate.Server.Services.Magic.Spells.Magery.Second;
using Moongate.Server.Services.Magic.Spells.Magery.Sixth;
using Moongate.Server.Services.Magic.Spells.Magery.Third;
using Moongate.Server.Types.Magic;

namespace Moongate.Server.Services.Magic;

/// <summary>
/// Registers built-in spells with the shared spell registry.
/// </summary>
public static class SpellInitializer
{
    public static void RegisterAll(SpellRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.Register(new ClumsySpell());
        registry.Register(new AgilitySpell());
        registry.Register(new FeeblemindSpell());
        registry.Register(new FireballSpell());
        registry.Register(new HealSpell());
        registry.Register(new MagicArrowSpell());
        registry.Register(new NightSightSpell());
        registry.Register(new ParalyzeSpell());
        registry.Register(new ExplosionSpell());
        registry.Register(new GreaterHealSpell());
        registry.Register(new ReactiveArmorSpell());
        registry.Register(new WeakenSpell());
    }
}
