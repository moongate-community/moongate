using Moongate.Core.Directories;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Every shipped vendor template that runs <c>shopkeeper.lua</c> (which offers Buy and Sell) must be in a shipped
///     shop, else Buy, Sell and "vendor buy" do nothing, silently.
/// </summary>
public sealed class ShippedShopCoverageTests
{
    [Fact]
    public async Task EveryShippedShopkeeper_HasAShop()
    {
        var directories = new DirectoriesConfig(Path.Combine(RepositoryRoot(), "moongate_root"), ["templates"]);
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            ).LoadDataAsync())
            .Entities.ToArray();
        var shops = (await new ShopsLoader(directories, new StubDataLoaderService().With(items).With(mobiles))
                .LoadDataAsync())
            .Entities.ToArray();
        var service = new ShopService(new StubDataLoaderService().With(shops));

        var orphans = mobiles
            .Where(template => template.ScriptId == "shopkeeper" && template.Id != "basevendor")
            .Where(template => !service.TryGetFor(template.Id, out _))
            .Select(template => template.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"{orphans.Length} shipped templates run shopkeeper.lua but are in no shop: {string.Join(", ", orphans)}"
        );
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Moongate.slnx was not found above the tests.");
    }
}
