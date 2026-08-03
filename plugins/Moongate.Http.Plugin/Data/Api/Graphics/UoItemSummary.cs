using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Data.Api.Graphics;

/// <summary>
/// One row of the raw item catalogue: a tile as the client files describe it, before any template has
/// had an opinion about it.
/// </summary>
public sealed record UoItemSummary(
    int ItemId,
    string Hex,
    string Name,
    IReadOnlyList<string> Flags,
    int Weight,
    int Height,
    string ImageUrl
)
{
    /// <summary>Projects one tiledata entry into its catalogue row.</summary>
    public static UoItemSummary From(int itemId, ItemData data)
        => new(
            itemId,
            $"0x{itemId:X4}",
            data.Name?.Trim() ?? string.Empty,
            FlagsOf(data.Flags),
            data.Weight,
            data.Height,
            $"/api/v1/images/items/0x{itemId:X4}.png"
        );

    /// <summary>The set flags by name, which is how a caller filters and how a tooltip reads.</summary>
    public static IReadOnlyList<string> FlagsOf(TileFlagType flags)
        =>
        [
            .. Enum.GetValues<TileFlagType>()
                   .Where(flag => flag != TileFlagType.None && (flags & flag) != 0)
                   .Select(flag => flag.ToString())
        ];
}
