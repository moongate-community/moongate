using Moongate.Server.Interfaces.Services.Entities;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Entities;

public sealed class MobileModifierAggregationService : IMobileModifierAggregationService
{
    public MobileModifiers RecalculateEquipmentModifiers(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        var total = new MobileModifiers();

        foreach (var item in mobile.GetEquippedItemsRuntime())
        {
            var modifiers = item.Modifiers;

            if (modifiers is null)
            {
                continue;
            }

            total.StrengthBonus += modifiers.StrengthBonus;
            total.DexterityBonus += modifiers.DexterityBonus;
            total.IntelligenceBonus += modifiers.IntelligenceBonus;
            total.PhysicalResist += modifiers.PhysicalResist;
            total.FireResist += modifiers.FireResist;
            total.ColdResist += modifiers.ColdResist;
            total.PoisonResist += modifiers.PoisonResist;
            total.EnergyResist += modifiers.EnergyResist;
            total.HitChanceIncrease += modifiers.HitChanceIncrease;
            total.DefenseChanceIncrease += modifiers.DefenseChanceIncrease;
            total.DamageIncrease += modifiers.DamageIncrease;
            total.SwingSpeedIncrease += modifiers.SwingSpeedIncrease;
            total.SpellDamageIncrease += modifiers.SpellDamageIncrease;
            total.FasterCasting += modifiers.FasterCasting;
            total.FasterCastRecovery += modifiers.FasterCastRecovery;
            total.LowerManaCost += modifiers.LowerManaCost;
            total.LowerReagentCost += modifiers.LowerReagentCost;
            total.Luck += modifiers.Luck;
            total.SpellChanneling += modifiers.SpellChanneling;
        }

        mobile.EquipmentModifiers = total;

        return total;
    }
}
