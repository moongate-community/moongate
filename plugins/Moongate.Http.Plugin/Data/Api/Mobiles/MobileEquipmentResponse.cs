using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Http.Plugin.Data.Api.Mobiles;

/// <summary>One item a template spawns its mobile wearing.</summary>
/// <param name="Item">The item template id to spawn.</param>
/// <param name="Layer">The layer it goes on.</param>
/// <param name="Hue">A hue spec, or null to leave the item its own colour.</param>
public sealed record MobileEquipmentResponse(string Item, string Layer, string? Hue)
{
    /// <summary>Projects one equipment entry.</summary>
    public static MobileEquipmentResponse From(MobileEquipmentEntry entry)
        => new(entry.Item, entry.Layer, entry.Hue);
}
