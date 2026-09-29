using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Tiles;

namespace Moongate.Tests.TestSupport.Ultima.Tooltips;

/// <summary>
///     A real <see cref="TooltipService" /> over the given live items and mobiles, with no templates or messages.
/// </summary>
public static class TestTooltips
{
    public static TooltipService Create(IItemService items, IMobileService mobiles)
    {
        var data = new StubDataLoaderService();

        return new(
            new ItemTemplateService(data),
            new FakeTileDataService(),
            new LocalizationService(new LocalizationConfig(), data),
            items,
            mobiles,
            new WorldConfig()
        );
    }
}
