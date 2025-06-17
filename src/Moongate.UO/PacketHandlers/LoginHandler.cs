using System.Net;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.UO.Data.Packets;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Session;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services;
using Serilog;

namespace Moongate.UO.PacketHandlers;

public class LoginHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<LoginHandler>();

    private readonly IAccountService _accountService;

    private readonly ShardListPacket _shareListPacket = new();

    private readonly MoongateServerConfig _serverConfig;

    public LoginHandler(IAccountService accountService, MoongateServerConfig serverConfig)
    {
        _accountService = accountService;
        _serverConfig = serverConfig;


        // TODO: Get address
        _shareListPacket.AddShard(new GameServerEntry()
        {
            ServerName = _serverConfig.Name,
            Index = 0,
            IpAddress = IPAddress.Parse("127.0.0.1")
        });

    }

    public async Task HandlePacketAsync(GameNetworkSession session, IUoNetworkPacket packet)
    {
        if (packet is LoginSeedPacket seedPacket)
        {
            await HandleLoginSeedAsync(session, seedPacket);
            return;
        }

        if (packet is LoginRequestPacket loginRequestPacket)
        {
            await HandleLoginRequestAsync(session, loginRequestPacket);
            return;
        }
    }

    private Task HandleLoginSeedAsync(GameNetworkSession session, LoginSeedPacket packet)
    {
        _logger.Debug("Received login seed {Seed} from {SessionId}", packet.Seed, session.SessionId);

        session.Seed = packet.Seed;
        return Task.CompletedTask;
    }

    private async Task HandleLoginRequestAsync(GameNetworkSession session, LoginRequestPacket packet)
    {
        _logger.Debug("Received login request from {SessionId} with username {Username}", session.SessionId, packet.Account);


        var loginResult = await _accountService.LoginAsync(packet.Account, packet.Password);

        if (!loginResult.IsSuccess)
        {
            _logger.Warning("Login failed for {Username}: {ErrorMessage}", packet.Account, loginResult.ErrorMessage);
            session.SendPackets(new LoginDeniedPacket(LoginDeniedReason.IncorrectNameOrPassword));
            return;
        }

        var account = loginResult.Value;

        if (!account.IsActive)
        {
            _logger.Warning("Login denied for {Username}: Account is inactive", packet.Account);
            session.SendPackets(new LoginDeniedPacket(LoginDeniedReason.AccountBlocked));
            return;
        }

        // TODO: Check if account is in use by another session

        _logger.Information("Login successful for {Username} on session {SessionId}", packet.Account, session.SessionId);
        session.Account = account;

        session.SendPackets(_shareListPacket);

    }
}
