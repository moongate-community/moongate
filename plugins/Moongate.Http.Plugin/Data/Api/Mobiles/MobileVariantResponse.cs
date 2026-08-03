using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>One variant of a template: a weighted alternative look, name pool and loot.</summary>
/// <param name="Weight">Its share of the draw, relative to the other variants.</param>
/// <param name="Gender">The gender name, or null when the variant does not fix one.</param>
public sealed record MobileVariantResponse(
    string Name,
    int Weight,
    string? Gender,
    string? NamePool,
    string? LootTableId,
    MobileAppearanceResponse Appearance,
    IReadOnlyList<MobileEquipmentResponse> Equipment
)
{
    /// <summary>Projects one variant, its own appearance and equipment included.</summary>
    public static MobileVariantResponse From(MobileVariant variant)
        => new(
            variant.Name,
            variant.Weight,
            variant.Gender?.ToString(),
            variant.NamePool,
            variant.LootTableId,
            MobileAppearanceResponse.From(variant.Appearance),
            [.. variant.Equipment.Select(MobileEquipmentResponse.From)]
        );
}
