using DryIoc;
using Moongate.Server;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Loaders;
using Moongate.Server.Plugins;
using Moongate.Server.Services.Items;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server;

public class MoongateDataLoaderPluginTests
{
    [Fact]
    public void Configure_RegistersLootServiceAfterItemLoader()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-plugin-" + Guid.NewGuid().ToString("N"));
        var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(root, []));

        new MoongateDataLoaderPlugin().Configure(container, new());

        var service = container.Resolve<ILootTemplateService>();
        var loaders = container.Resolve<IReadOnlyList<IDataLoader>>();
        var itemIndex = loaders.ToList().FindIndex(loader => loader is ItemTemplatesLoader);
        var lootIndex = loaders.ToList().FindIndex(loader => loader is LootTemplatesLoader);

        Assert.IsType<LootTemplateService>(service);
        Assert.True(itemIndex >= 0);
        Assert.True(lootIndex > itemIndex);
    }
}
