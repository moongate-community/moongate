using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>One row of the staff body picker.</summary>
public sealed record BodySummary(int Body, string Hex, string Type, string ImageUrl)
{
    /// <summary>Projects a classified body into its picker row.</summary>
    public static BodySummary From(int body, MobType type)
        => new(body, $"0x{body:X4}", type.ToString(), $"/api/v1/images/bodies/{body}.png");
}
