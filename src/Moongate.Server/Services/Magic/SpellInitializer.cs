using Moongate.Server.Services.Magic.Spells.Magery.First;
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
        registry.Register(new FeeblemindSpell());
        registry.Register(new HealSpell());
        registry.Register(new MagicArrowSpell());
        registry.Register(new NightSightSpell());
        registry.Register(new ReactiveArmorSpell());
        registry.Register(new WeakenSpell());
    }
}
