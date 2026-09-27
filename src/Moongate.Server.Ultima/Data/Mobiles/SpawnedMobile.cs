using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     A mobile <see cref="Interfaces.IMobileFactoryService.SpawnAsync" /> put in the world: what it wears (without the
///     backpack), its backpack, and what the backpack holds.
/// </summary>
public sealed record SpawnedMobile(
    MobileEntity Mobile,
    IReadOnlyList<ItemEntity> Equipment,
    ItemEntity Backpack,
    IReadOnlyList<ItemEntity> BackpackItems
);
