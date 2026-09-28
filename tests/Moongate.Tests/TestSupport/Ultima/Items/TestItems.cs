using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Sectors;

namespace Moongate.Tests.TestSupport.Ultima.Items;

/// <summary>
///     Builds an <see cref="ItemService" /> over its own sector grid unless one is given.
/// </summary>
public static class TestItems
{
    public static ItemService Create(ISectorService? sectors = null)
    {
        return new(sectors ?? TestSectors.Create());
    }
}
