using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>One row of the staff mobile template listing.</summary>
/// <param name="Body">The animation body id, which is what the picture is drawn from.</param>
/// <param name="VariantCount">How many weighted alternatives the template carries.</param>
public sealed record MobileTemplateSummaryResponse(
    string Id,
    string Name,
    string Title,
    string Category,
    IReadOnlyList<string> Tags,
    int Body,
    int Strength,
    int Dexterity,
    int Intelligence,
    int VariantCount,
    string ImageUrl
)
{
    /// <summary>Projects a template into its listing row, figure url included.</summary>
    public static MobileTemplateSummaryResponse From(MobileTemplate template)
        => new(
            template.Id,
            template.Name,
            template.Title,
            template.Category,
            template.Tags,
            template.Appearance.Body,
            template.Strength,
            template.Dexterity,
            template.Intelligence,
            template.Variants.Count,
            MobileTemplateUrls.Figure(template.Id)
        );
}
