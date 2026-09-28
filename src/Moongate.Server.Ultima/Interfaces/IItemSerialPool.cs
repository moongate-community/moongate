using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Item serials already reserved from the database, so an item made on the game loop, such as the rest of a split
///     stack, gets its serial without waiting on the database.
/// </summary>
public interface IItemSerialPool : IMoongateStartupService
{
    /// <summary>
    ///     Takes a reserved serial; false when the pool is empty, which starts a refill.
    /// </summary>
    bool TryTake(out Serial serial);
}
