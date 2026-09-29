using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Sectors;

/// <summary>
///     What lies around a point: the player characters, the NPCs and the items on the ground.
/// </summary>
public sealed record SectorQueryResult(
    IReadOnlyList<MobileEntity> Players,
    IReadOnlyList<MobileEntity> Npcs,
    IReadOnlyList<ItemEntity> Items
);
