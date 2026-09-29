using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.World;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Builds an <see cref="ItemService" /> over its own sector grid, a movement stub that lands drops at 0, a line of
///     sight that sees everything, an empty recording data access and an inline game loop, unless others are given.
/// </summary>
public static class TestItems
{
    public static ItemService Create(
        ISectorService? sectors = null,
        IMovementService? movement = null,
        ILineOfSightService? sight = null,
        IDataAccess<ItemEntity>? data = null,
        IGameLoopService? loop = null,
        IItemScriptService? scripts = null
    )
    {
        return new(
            sectors ?? TestSectors.Create(),
            movement ?? new StubMovementService(),
            sight ?? new StubLineOfSightService(),
            data ?? new RecordingDataAccess<ItemEntity>(),
            loop ?? new StubGameLoop(),
            scripts
        );
    }
}
