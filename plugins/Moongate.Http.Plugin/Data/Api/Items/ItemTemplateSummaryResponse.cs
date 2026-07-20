using Moongate.UO.Data.Items;

namespace Moongate.Http.Plugin.Data.Api.Items;

/// <summary>One row of the staff item template listing.</summary>
public sealed record ItemTemplateSummaryResponse(
    string Id,
    string Name,
    string Category,
    int ItemId,
    int Hue,
    string Rarity,
    int GoldValue,
    double Weight,
    IReadOnlyList<string> Tags,
    string ImageUrl
)
{
    /// <summary>Projects a template into its listing row, art url included.</summary>
    public static ItemTemplateSummaryResponse From(ItemTemplate template)
        => new(
            template.Id,
            template.Name,
            template.Category,
            template.ItemId,
            template.Hue,
            template.Rarity.ToString(),
            template.GoldValue,
            template.Weight,
            template.Tags,
            $"/api/v1/images/items/0x{template.ItemId:x4}.png"
        );
}
