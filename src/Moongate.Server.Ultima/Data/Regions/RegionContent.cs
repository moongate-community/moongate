using Moongate.Core.Geometry;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Regions;

/// <summary>
///     One region of a <c>data/regions/&lt;map&gt;.toml</c> file. Every rule is written out, already combined with the
///     region's parents, so a region is read on its own.
/// </summary>
/// <remarks>
///     Where regions overlap, the one with the highest <see cref="Priority" /> gives the name, music, guards,
///     housing and logout rules. Travel is different: a travel spell is blocked when any region covering the place
///     blocks it. Travel zones use this: they are unnamed regions of priority 0 that only limit travel.
/// </remarks>
public class RegionContent
{
    /// <summary>
    ///     The map of the region, taken from the name of its file.
    /// </summary>
    public MapType Map { get; set; }

    /// <summary>
    ///     The region name; unnamed regions only apply their rules.
    /// </summary>
    public string? Name { get; set; }

    public RegionType Type { get; set; }

    /// <summary>
    ///     Where regions overlap, the one with the highest priority applies.
    /// </summary>
    public int Priority { get; set; } = 50;

    /// <summary>
    ///     The name of the region this one is part of, on the same map.
    /// </summary>
    public string? Parent { get; set; }

    public List<RegionAreaContent> Areas { get; set; } = [];

    /// <summary>
    ///     Where a "go to region" command takes a character.
    /// </summary>
    public Point3D? GoLocation { get; set; }

    /// <summary>
    ///     The entrance of a dungeon or town.
    /// </summary>
    public Point3D? Entrance { get; set; }

    public MusicType? Music { get; set; }

    /// <summary>
    ///     The name of the weather profile of <c>weather.toml</c> the region uses.
    /// </summary>
    public string Weather { get; set; } = "none";

    /// <summary>
    ///     The name a rune marked here gets.
    /// </summary>
    public string? RuneName { get; set; }

    /// <summary>
    ///     Whether guards protect the region.
    /// </summary>
    public bool Guarded { get; set; }

    /// <summary>
    ///     Whether players may place houses in the region.
    /// </summary>
    public bool Housing { get; set; } = true;

    /// <summary>
    ///     Whether a character with no fight in progress leaves the world at once when logging out here.
    /// </summary>
    public bool InstantLogout { get; set; }

    public bool RecallIn { get; set; } = true;

    public bool RecallOut { get; set; } = true;

    public bool GateIn { get; set; } = true;

    public bool GateOut { get; set; } = true;

    /// <summary>
    ///     Whether a rune can be marked in the region.
    /// </summary>
    public bool Mark { get; set; } = true;

    /// <summary>
    ///     Whether the Teleport spell can move a character into the region.
    /// </summary>
    public bool TeleportIn { get; set; } = true;

    /// <summary>
    ///     Whether the Teleport spell can move a character out of the region.
    /// </summary>
    public bool TeleportOut { get; set; } = true;

    /// <summary>
    ///     Returns whether the point is inside one of the region's areas.
    /// </summary>
    public bool Contains(int x, int y, int z)
    {
        return Areas.Any(area => area.Contains(x, y, z));
    }
}
