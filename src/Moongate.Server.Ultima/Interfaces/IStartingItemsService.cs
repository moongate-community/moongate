using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Gives a new character its backpack, the items of <c>data/starting_items.toml</c> and its starting gold.
/// </summary>
public interface IStartingItemsService : IMoongateStartupService
{
    /// <summary>
    ///     Creates and saves every starting item of the character in one transaction; nothing is saved when one fails.
    ///     Returns them, the backpack first.
    /// </summary>
    Task<IReadOnlyList<ItemEntity>> GiveAsync(StartingItemsRequest request, CancellationToken cancellationToken = default);
}
