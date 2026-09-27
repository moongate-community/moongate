using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Mobiles;

/// <summary>
///     A mobile <see cref="Interfaces.IMobileFactoryService.SpawnAsync" /> put in the world, with the items it wears.
/// </summary>
public sealed record SpawnedMobile(MobileEntity Mobile, IReadOnlyList<ItemEntity> Equipment);
