using Moongate.UO.Data.Items;

namespace Moongate.Http.Plugin.Data.Api.Items;

/// <summary>
/// The full template: everything the summary carries plus the typed specs, passed through as the
/// Moongate.UO.Data types — they are the documented data contract of the YAML templates.
/// </summary>
public sealed record ItemTemplateResponse(
    string Id,
    string Name,
    string Category,
    string Description,
    int ItemId,
    int Hue,
    string Rarity,
    int GoldValue,
    double Weight,
    string ScriptId,
    bool IsMovable,
    bool? Stackable,
    bool? Dyeable,
    string? Visibility,
    string? LootType,
    IReadOnlyList<string> Tags,
    IReadOnlyList<int>? FlippableItemIds,
    IReadOnlyList<string>? LootTables,
    IReadOnlyDictionary<string, ItemParam>? Params,
    EquipSpec? Equip,
    WeaponSpec? Weapon,
    ContainerSpec? Container,
    BookSpec? Book,
    string ImageUrl
)
{
    /// <summary>Projects a template into its full response, art url included.</summary>
    public static ItemTemplateResponse From(ItemTemplate template)
        => new(
            template.Id,
            template.Name,
            template.Category,
            template.Description,
            template.ItemId,
            template.Hue,
            template.Rarity.ToString(),
            template.GoldValue,
            template.Weight,
            template.ScriptId,
            template.IsMovable,
            template.Stackable,
            template.Dyeable,
            template.Visibility,
            template.LootType,
            template.Tags,
            template.FlippableItemIds,
            template.LootTables,
            template.Params,
            template.Equip,
            template.Weapon,
            template.Container,
            template.Book,
            $"/api/v1/images/items/0x{template.ItemId:x4}.png"
        );
}
