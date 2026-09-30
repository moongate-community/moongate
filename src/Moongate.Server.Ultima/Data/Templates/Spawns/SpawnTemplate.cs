using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Templates.Spawns;

/// <summary>
///     A spawn region, converted from UOX3's <c>[REGIONSPAWN n]</c>: up to <see cref="Max" /> NPCs of its mobiles or
///     lists live in its areas; a new one comes every <see cref="MinMinutes" /> to <see cref="MaxMinutes" />.
/// </summary>
public class SpawnTemplate
{
    /// <summary>
    ///     Unique on its map, such as <c>britain_0</c>.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The map, taken from the folder of the file (<c>templates/spawns/felucca/</c>).
    /// </summary>
    public MapType Map { get; set; }

    /// <summary>
    ///     A readable name, such as <c>The Hammer And Anvil</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The mobile templates spawned, picked at random with the entries of <see cref="NpcListIds" />.
    /// </summary>
    public List<string> MobileIds { get; set; } = [];

    /// <summary>
    ///     The NPC lists spawned from.
    /// </summary>
    public List<string> NpcListIds { get; set; } = [];

    /// <summary>
    ///     How many NPCs of the region live at once.
    /// </summary>
    public int Max { get; set; } = 1;

    public int MinMinutes { get; set; }

    public int MaxMinutes { get; set; }

    /// <summary>
    ///     How many NPCs come at a time, up to <see cref="Max" />.
    /// </summary>
    public int Call { get; set; } = 1;

    public List<SpawnArea> Areas { get; set; } = [];

    /// <summary>
    ///     Parts of the areas where nothing spawns.
    /// </summary>
    public List<SpawnArea> Exclude { get; set; } = [];

    /// <summary>
    ///     How far above the ground a spawn spot may be, on a static; 18 when unset.
    /// </summary>
    public int? PrefZ { get; set; }

    /// <summary>
    ///     A fixed height for every spot, instead of the ground's.
    /// </summary>
    public int? Z { get; set; }

    /// <summary>
    ///     Whether NPCs spawn only outside buildings (under no roof).
    /// </summary>
    public bool OnlyOutside { get; set; }
}
