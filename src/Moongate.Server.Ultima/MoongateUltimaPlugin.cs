using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.General;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Handlers.Login;
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
        container.GetDirectoriesConfig().CreateDirectoryIfNotExists("templates");
        container.GetDirectoriesConfig().CreateDirectoryIfNotExists("templates/mobiles/");
        container.GetDirectoriesConfig().CreateDirectoryIfNotExists("templates/items/");
        container.GetDirectoriesConfig().CreateDirectoryIfNotExists("templates/loots/");

        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());

        var mode = container.IsRegistered<ServerMode>() ? container.Resolve<ServerMode>() : ServerMode.Standalone;

        if ((mode & ServerMode.Login) != 0)
        {
            container.AddPersistenceAuth<AccountEntity>();
            container.AddMoongateService<IAccountService, AccountService>();
            container.Register<IAccountAdminAccessService, AccountAdminAccessService>(Reuse.Singleton);
            container.Register<LoginAccountFlow>(Reuse.Singleton);
            container.RegisterCommand<AccountCommand>(
                "account",
                "Creates an account: account create <username> <password> [Regular|GameMaster|Administrator].",
                CommandSourceType.Console | CommandSourceType.InGame,
                AccountType.Administrator
            );

            container.RegisterLoginPacketHandler<PingPacket, LoginRolePingPacketHandler>();
            container.RegisterLoginPacketHandler<LoginSeedPacket, LoginRoleSeedPacketHandler>();
            container.RegisterLoginPacketHandler<ClientVersionPacket, LoginRoleClientVersionPacketHandler>();
            container.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
            container.RegisterLoginPacketHandler<ServerSelectPacket, LoginRoleServerSelectPacketHandler>();
        }

        if ((mode & ServerMode.Game) != 0)
        {
            container.RegisterPacketHandler<LoginSeedPacket, LoginSeedPacketHandler>();
            container.RegisterAsyncPacketHandler<GameLoginPacket, GameLoginPacketHandler>();

            // After IUltimaDataService (-10): loaders read MUL/UOP files after Files.SetDirectory.
            container.AddMoongateService<IDataLoaderService, DataLoaderService>(-5);
        }
    }
}
