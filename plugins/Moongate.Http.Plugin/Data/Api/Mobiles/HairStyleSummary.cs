using Moongate.Http.Plugin.Data.Mobiles;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>One row of the staff hair-style picker.</summary>
public sealed record HairStyleSummary(int Style, string Hex, string Name, bool Facial, string ImageUrl)
{
    /// <summary>Projects a catalog entry into its picker row.</summary>
    public static HairStyleSummary From(HairStyleEntry entry)
        => new(
            entry.Style,
            entry.Hex,
            entry.Name,
            entry.Facial,
            $"/api/v1/images/hair/{entry.Style}.png{(entry.Facial ? "?facial=true" : string.Empty)}"
        );
}
