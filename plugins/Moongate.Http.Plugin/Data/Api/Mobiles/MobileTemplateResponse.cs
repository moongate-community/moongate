using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>One mobile spawn template in full.</summary>
/// <param name="NamePool">The pool a spawn draws its name from when the template has no fixed one.</param>
/// <param name="Gender">The gender name, or null when the template lets a spawn pick.</param>
/// <param name="BaseMobile">The template this one merges over, or null when it stands alone.</param>
/// <param name="Skills">Skill values by name, as the template sets them.</param>
/// <param name="Variants">Weighted alternatives, each with its own appearance and equipment.</param>
public sealed record MobileTemplateResponse(
    string Id,
    string Name,
    string NamePool,
    string? Gender,
    string Title,
    string Category,
    string Description,
    IReadOnlyList<string> Tags,
    string? BaseMobile,
    int Strength,
    int Dexterity,
    int Intelligence,
    IReadOnlyDictionary<string, int> Skills,
    MobileAppearanceResponse Appearance,
    IReadOnlyList<MobileEquipmentResponse> Equipment,
    IReadOnlyList<MobileVariantResponse> Variants,
    string? LootTableId,
    string? BrainScript,
    string ImageUrl,
    string PaperdollUrl
)
{
    /// <summary>Projects a template in full, both picture urls included.</summary>
    public static MobileTemplateResponse From(MobileTemplate template)
        => new(
            template.Id,
            template.Name,
            template.NamePool,
            template.Gender?.ToString(),
            template.Title,
            template.Category,
            template.Description,
            template.Tags,
            template.BaseMobile,
            template.Strength,
            template.Dexterity,
            template.Intelligence,
            template.Skills,
            MobileAppearanceResponse.From(template.Appearance),
            [.. template.Equipment.Select(MobileEquipmentResponse.From)],
            [.. template.Variants.Select(MobileVariantResponse.From)],
            template.LootTableId,
            template.BrainScript,
            MobileTemplateUrls.Figure(template.Id),
            MobileTemplateUrls.Paperdoll(template.Id)
        );
}
