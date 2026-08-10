using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.World;

namespace Moongate.Server.Services.World;

/// <summary>The world's decoration, as loaded from the tracked YAML under Assets.</summary>
public sealed class DecorationCatalog : IDecorationCatalog
{
    private readonly List<DecorationPlacement> _placements = [];

    /// <inheritdoc />
    public IReadOnlyList<DecorationPlacement> All => _placements;

    /// <inheritdoc />
    public void Add(int mapId, IEnumerable<DecorationGroup> groups)
        => _placements.AddRange(Expand(mapId, groups));

    /// <summary>
    /// Turns groups into the individual objects the world will hold. A coordinate that is not an
    /// [x, y, z] triple is skipped rather than thrown on: this is a hundred data files, and one bad
    /// line should cost one object, not the whole facet.
    /// </summary>
    public static IReadOnlyList<DecorationPlacement> Expand(int mapId, IEnumerable<DecorationGroup> groups)
    {
        var placements = new List<DecorationPlacement>();

        foreach (var group in groups)
        {
            foreach (var at in group.At)
            {
                if (at.Length != 3)
                {
                    continue;
                }

                placements.Add(new(group.Type, group.ItemId, group.Hue, mapId, new(at[0], at[1], at[2])));
            }
        }

        return placements;
    }
}
