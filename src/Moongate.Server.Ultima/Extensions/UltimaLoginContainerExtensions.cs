using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Server.Ultima.Services;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers the login role: accounts, their command and the login packet handlers.
/// </summary>
public static class UltimaLoginContainerExtensions
{
    /// <summary>
    ///     Registers the login role: accounts, their command and the login packet handlers.
    /// </summary>
    public static Container AddUltimaLoginRole(this Container container)
    {
        container.AddPersistenceAuth<AccountEntity>();
        container.AddMoongateService<IAccountService, AccountService>();
        container.Register<IAccountAdminAccessService, AccountAdminAccessService>(Reuse.Singleton);
        container.Register<LoginAccountFlow>(Reuse.Singleton);
        container.RegisterCommand<AccountCommand>(
            "account",
            "Creates an account: account create <username> <password> [Regular|GameMaster|Administrator]. Console provisioning: account api-access <username> <on|off>.",
            CommandSourceType.Console | CommandSourceType.InGame,
            descriptionMessage: CommandMessages.AccountDescription
        );

        container.RegisterLoginPacketHandler<PingPacket, LoginRolePingPacketHandler>();
        container.RegisterLoginPacketHandler<LoginSeedPacket, LoginRoleSeedPacketHandler>();
        container.RegisterLoginPacketHandler<ClientVersionPacket, LoginRoleClientVersionPacketHandler>();
        container.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
        container.RegisterLoginPacketHandler<ServerSelectPacket, LoginRoleServerSelectPacketHandler>();

        // The Enhanced Client sends its hardware information right after the account login, before the server list.
        container.RegisterLoginPacketHandler<ClientHardwareInfoPacket, LoginRoleIgnoredPacketHandler<ClientHardwareInfoPacket>>();

        return container;
    }
}
