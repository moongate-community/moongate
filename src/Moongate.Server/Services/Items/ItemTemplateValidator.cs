using Moongate.Server.Data.Internal;
using Moongate.UO.Data.Items;

namespace Moongate.Server.Services.Items;

internal static class ItemTemplateValidator
{
    public static void Validate(IReadOnlyList<ItemTemplateSource> sources)
    {
        var observedIds = new Dictionary<string, ItemTemplateSource>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            var template = source.Template;

            ValidateRequired(source, template.Id, nameof(ItemTemplate.Id));
            ValidateRequired(source, template.Name, nameof(ItemTemplate.Name));
            ValidateRequired(source, template.Category, nameof(ItemTemplate.Category));

            if (!observedIds.TryAdd(template.Id, source))
            {
                throw Error(source, nameof(ItemTemplate.Id), $"Duplicate item template ID '{template.Id}'.");
            }

            ValidateNonNegative(source, template.ItemId, nameof(ItemTemplate.ItemId));
            ValidateNonNegative(source, template.Hue, nameof(ItemTemplate.Hue));
            ValidateNonNegative(source, template.GoldValue, nameof(ItemTemplate.GoldValue));
            ValidateFinite(source, template.Weight, nameof(ItemTemplate.Weight));
            ValidateNonNegative(source, template.Weight, nameof(ItemTemplate.Weight));
            ValidateValues(source, template.FlippableItemIds, nameof(ItemTemplate.FlippableItemIds));
            ValidateEquip(source, template.Equip);
            ValidateWeapon(source, template.Weapon);
            ValidateContainer(source, template.Container);
        }
    }

    private static InvalidDataException Error(ItemTemplateSource source, string property, string message)
    {
        var templateId = string.IsNullOrWhiteSpace(source.Template.Id) ? "<unknown>" : source.Template.Id;

        return new($"{source.RelativePath}: item '{templateId}', property '{property}': {message}");
    }

    private static void ValidateContainer(ItemTemplateSource source, ContainerSpec? container)
    {
        if (container is null)
        {
            return;
        }

        ValidateNonNegative(source, container.WeightMax, "Container.WeightMax");
        ValidateNonNegative(source, container.MaxItems, "Container.MaxItems");
        ValidateNonNegative(source, container.GumpId, "Container.GumpId");
        ValidateNonNegative(source, container.WeightReduction, "Container.WeightReduction");
        ValidateNonNegative(source, container.QuiverDamageIncrease, "Container.QuiverDamageIncrease");
        ValidateNonNegative(source, container.LowerAmmoCost, "Container.LowerAmmoCost");
        ValidateNonNegative(source, container.DefenseChanceIncrease, "Container.DefenseChanceIncrease");
    }

    private static void ValidateEquip(ItemTemplateSource source, EquipSpec? equip)
    {
        if (equip is null)
        {
            return;
        }

        ValidateNonNegative(source, equip.HitPoints, "Equip.HitPoints");
        ValidateNonNegative(source, equip.StrengthReq, "Equip.StrengthReq");
        ValidateNonNegative(source, equip.DexterityReq, "Equip.DexterityReq");
        ValidateNonNegative(source, equip.IntelligenceReq, "Equip.IntelligenceReq");
    }

    private static void ValidateFinite(ItemTemplateSource source, double value, string property)
    {
        if (!double.IsFinite(value))
        {
            throw Error(source, property, $"{property} must be finite.");
        }
    }

    private static void ValidateNonNegative(ItemTemplateSource source, double? value, string property)
    {
        if (value < 0)
        {
            throw Error(source, property, $"{property} cannot be negative.");
        }
    }

    private static void ValidateRequired(ItemTemplateSource source, string? value, string property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Error(source, property, $"{property} is required.");
        }
    }

    private static void ValidateValues(ItemTemplateSource source, IReadOnlyList<int>? values, string property)
    {
        if (values is null)
        {
            return;
        }

        for (var index = 0; index < values.Count; index++)
        {
            ValidateNonNegative(source, values[index], $"{property}[{index}]");
        }
    }

    private static void ValidateWeapon(ItemTemplateSource source, WeaponSpec? weapon)
    {
        if (weapon is null)
        {
            return;
        }

        ValidateNonNegative(source, weapon.LowDamage, "Weapon.LowDamage");
        ValidateNonNegative(source, weapon.HighDamage, "Weapon.HighDamage");
        ValidateNonNegative(source, weapon.Speed, "Weapon.Speed");
        ValidateNonNegative(source, weapon.BaseRange, "Weapon.BaseRange");
        ValidateNonNegative(source, weapon.MaxRange, "Weapon.MaxRange");
        ValidateNonNegative(source, weapon.HitSound, "Weapon.HitSound");
        ValidateNonNegative(source, weapon.MissSound, "Weapon.MissSound");
        ValidateNonNegative(source, weapon.Ammo, "Weapon.Ammo");
        ValidateNonNegative(source, weapon.AmmoFx, "Weapon.AmmoFx");
    }
}
