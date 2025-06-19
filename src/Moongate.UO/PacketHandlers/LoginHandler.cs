using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using Moongate.Core.Server.Data.Configs.Server;
using Moongate.Core.Server.Interfaces.Packets;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.UO.Data.Events.Features;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Packets;
using Moongate.UO.Data.Packets.Characters;
using Moongate.UO.Data.Packets.Data;
using Moongate.UO.Data.Packets.Login;
using Moongate.UO.Data.Packets.Sessions;
using Moongate.UO.Data.Session;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;
using Moongate.UO.Interfaces.Handlers;
using Moongate.UO.Interfaces.Services;
using Serilog;
using ZLinq;

namespace Moongate.UO.PacketHandlers;

public class LoginHandler : IGamePacketHandler
{
    private readonly ILogger _logger = Log.ForContext<LoginHandler>();

    private readonly IAccountService _accountService;

    private readonly ShardListPacket _shareListPacket = new();

    private readonly MoongateServerConfig _serverConfig;

    private readonly ISchedulerSystemService _schedulerSystemService;

    private readonly ConcurrentDictionary<uint, SessionInHoldObject> _sessionsInHold = new();

    public LoginHandler(
        IAccountService accountService, MoongateServerConfig serverConfig, ISchedulerSystemService schedulerSystemService
    )
    {
        _accountService = accountService;
        _serverConfig = serverConfig;
        _schedulerSystemService = schedulerSystemService;

        _schedulerSystemService.RegisterJob("sessionInHoldCleanup", CleanupSessionsInHoldAsync, TimeSpan.FromMinutes(1));


        // TODO: Get address
        _shareListPacket.AddShard(
            new GameServerEntry()
            {
                ServerName = _serverConfig.Name,
                Index = 0,
                IpAddress = IPAddress.Parse("127.0.0.1")
            }
        );
    }

    private Task CleanupSessionsInHoldAsync()
    {
        _logger.Debug("Cleaning up sessions in hold...");

        var now = DateTime.UtcNow;
        var threshold = now.AddMinutes(-5);

        foreach (var session in _sessionsInHold)
        {
            if (session.Value.AddedDatetime < threshold)
            {
                _sessionsInHold.TryRemove(session.Key, out _);
                _logger.Information("Removed session {SessionId} from hold due to inactivity", session.Key);
            }
        }

        return Task.CompletedTask;
    }

    public async Task HandlePacketAsync(GameSession session, IUoNetworkPacket packet)
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

        if (packet is SelectServerPacket selectServerPacket)
        {
            await HandleSelectServerAsync(session, selectServerPacket);
            return;
        }

        if (packet is GameServerLoginPacket gameServerLoginPacket)
        {
            await GameServerLoginPacket(session, gameServerLoginPacket);
            return;
        }
    }

    private async Task GameServerLoginPacket(GameSession session, GameServerLoginPacket packet)
    {
        if (_sessionsInHold.TryRemove(packet.AuthKey, out var sessionInHold))
        {
            _logger.Debug(
                "Auth key {AuthKey} found in hold for session {SessionId}",
                packet.AuthKey,
                session.SessionId
            );

            session.Account = await _accountService.GetAccountByIdAsync(sessionInHold.AccountId);
            session.SetState(NetworkSessionStateType.Authenticated);
            session.SetFeatures(NetworkSessionFeatureType.Compression);

            var characters = session.Account.Characters.AsValueEnumerable()
                .OrderBy(s => s.Slot)
                .Select(s => new CharacterEntry(s.Name))
                .ToList();
            var characterListPacket = new CharactersStartingLocationsPacket
            {
                Cities = StartingCities.AvailableStartingCities.ToList(),
            };

            characterListPacket.FillCharacters(!characters.Any() ? null : characters);




            session.SendPackets(new SupportFeaturesPacket());
            session.SendPackets(characterListPacket);


            return;
        }

        _logger.Warning("Auth key {AuthKey} not found in hold for session {SessionId}", packet.AuthKey, session.SessionId);
        session.Disconnect();
    }

    private Task HandleSelectServerAsync(GameSession session, SelectServerPacket packet)
    {
        _logger.Debug(
            "Received select server request from {SessionId} for server index {Index}",
            session.SessionId,
            packet.SelectedServerIndex
        );


        var authKey = (uint)new Random().Next(1, int.MaxValue);

        var connectToGameServer = new ConnectToGameServerPacket()
        {
            ServerAddress = IPEndPoint.Parse(session.NetworkClient.ServerId).Address,
            AuthKey = authKey,
            ServerPort = _serverConfig.Network.Port
        };

        _sessionsInHold.TryAdd(authKey, new SessionInHoldObject(authKey, session.Account.Id, DateTime.Now));
        session.SendPackets(connectToGameServer);

        return Task.CompletedTask;
    }


    private Task HandleLoginSeedAsync(GameSession session, LoginSeedPacket packet)
    {
        _logger.Debug("Received login seed {Seed} from {SessionId}", packet.Seed, session.SessionId);

        session.Seed = packet.Seed;
        return Task.CompletedTask;
    }

    private async Task HandleLoginRequestAsync(GameSession session, LoginRequestPacket packet)
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
