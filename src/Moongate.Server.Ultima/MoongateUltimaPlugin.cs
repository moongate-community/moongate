using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services;

namespace Moongate.Server.Ultima;

public class MoongateUltimaPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata
        => new(
            "com.github.moongate-community.moongate.plugins.ultima",
            "Moongate Ultima",
            new(1, 0),
            "squid",
            "Provides the core Ultima Online server functionality."
        );

    public void Register(Container container)
    {
        container
            .AddPersistenceAuth<AccountEntity>();

        container.AddMoongateService<IAccountService, AccountService>();

        // After IUltimaDataService (-10): loaders read MUL/UOP files, which need Files.SetDirectory
        // to already point at the configured client path.
        container.AddMoongateService<IDataLoaderService, DataLoaderService>(-5);
    }
}
