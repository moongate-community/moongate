using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Data.Titles;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Interfaces.Titles;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Plugins;

public sealed class MoongateUltimaPluginTitlesTests
{
    [Fact]
    public async Task Register_GameModeExposesLoaderAndTitleService()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/titles.toml", """
            [[titles]]
            fame = 0
            karma = 0
            title = "The Honored"
            """);
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["data"]));
        container.RegisterInstance(ServerMode.Game);
        container.RegisterMoongatePersistence(new());

        new MoongateUltimaPlugin().Register(container);

        var rows = (await container.Resolve<IDataLoader<FameKarmaTitle>>().LoadDataAsync()).Entities;
        Assert.Equal("The Honored", Assert.Single(rows).Title);
        Assert.NotNull(container.Resolve<IFameKarmaTitleService>());
    }
}
