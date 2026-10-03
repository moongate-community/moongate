using Moongate.Server.Ultima.Data.Templates.Spawns;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Data.Internal.Spawns;

/// <summary>
///     A spawn region at runtime: its template, what it picks its NPCs from and when it spawns next.
/// </summary>
internal sealed class SpawnRegionState
{
    public required SpawnTemplate Template { get; init; }

    /// <summary>
    ///     Gets what the NPCs are picked from; null for a region of items.
    /// </summary>
    public SpawnPool? Pool { get; init; }

    /// <summary>
    ///     Gets whether the region spawns items on the ground instead of NPCs.
    /// </summary>
    public bool OfItems => Template.ItemIds.Count > 0;

    public DateTimeOffset NextSpawn { get; set; }

    /// <summary>
    ///     Gets or sets whether the region has had its first spawn since the start, the one that fills it to its max.
    /// </summary>
    public bool Filled { get; set; }

    /// <summary>
    ///     Gets or sets whether the next spawn fills the region to its max, as <c>.initial_spawn</c> asks.
    /// </summary>
    public bool FillNow { get; set; }

    public bool Retrying { get; set; }
}
