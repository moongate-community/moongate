using System.Net;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Session;
using Moongate.Server.Data.Version;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Accounting;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
[RegisterPacketHandler(PacketDefinition.LoginSeedPacket), RegisterPacketHandler(PacketDefinition.AccountLoginPacket),
 RegisterPacketHandler(PacketDefinition.ServerSelectPacket), RegisterPacketHandler(PacketDefinition.GameLoginPacket),
 RegisterPacketHandler(PacketDefinition.LoginCharacterPacket)]

/// <summary>
/// Represents LoginHandler.
/// </summary>
public class LoginHandler : BasePacketListener, IGameEventListener<PlayerCharacterLoggedInEvent>
{
    private readonly ILogger _logger = Log.ForContext<LoginHandler>();

    private readonly IAccountService _accountService;
    private readonly ICharacterService _characterService;
    private readonly ServerListPacket _serverListPacket;
    private readonly IGameEventBusService _gameEventBusService;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly MoongateConfig _serverConfig;

    public LoginHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IAccountService accountService,
        ICharacterService characterService,
        IGameEventBusService gameEventBusService,
        MoongateConfig serverConfig,
        IGameNetworkSessionService gameNetworkSessionService
    ) : base(outgoingPacketQueue)
    {
        _accountService = accountService;
        _characterService = characterService;
        _gameEventBusService = gameEventBusService;
        _serverConfig = serverConfig;
        _gameNetworkSessionService = gameNetworkSessionService;
        _serverListPacket = new();
        _serverListPacket.Shards.Add(
            new()
            {
                Index = 0,
                IpAddress = IPAddress.Parse("127.0.0.1"),
                ServerName = "Moongate"
            }
        );

    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is LoginSeedPacket loginSeedPacket)
        {
            return await HandleLoginSeedPacketAsync(session, loginSeedPacket);
        }

        if (packet is AccountLoginPacket accountLoginPacket)
        {
            return await HandleAccountLoginPacketAsync(session, accountLoginPacket);
        }

        if (packet is ServerSelectPacket serverSelectPacket)
        {
            return await HandleServerSelectPacketAsync(session, serverSelectPacket);
        }

        if (packet is GameLoginPacket gameLoginPacket)
        {
            return await HandleGameLoginPacketAsync(session, gameLoginPacket);
        }

        if (packet is LoginCharacterPacket loginCharacterPacket)
        {
            return await HandleLoginCharacterPacketAsync(session, loginCharacterPacket);
        }

        return true;
    }

    private async Task<bool> HandleAccountLoginPacketAsync(GameSession session, AccountLoginPacket accountLoginPacket)
    {
        _logger.Information(
            "Received AccountLoginPacket from session {SessionId} with username {Username}",
            session.SessionId,
            accountLoginPacket.Account
        );

        var account = await _accountService.LoginAsync(accountLoginPacket.Account, accountLoginPacket.Password);

        if (account == null)
        {
            Enqueue(session, new LoginDeniedPacket(UOLoginDeniedReason.IncorrectNameOrPassword));

            return true;
        }

        session.AccountId = account.Id;
        session.AccountType = account.AccountType;

        Enqueue(session, _serverListPacket);

        return true;
    }

    private async Task<bool> HandleGameLoginPacketAsync(GameSession session, GameLoginPacket gameLoginPacket)
    {
        _logger.Information(
            "Received GameLoginPacket from session {SessionId} with account name {AccountName}",
            session.SessionId,
            gameLoginPacket.AccountName
        );

        var account = await _accountService.LoginAsync(gameLoginPacket.AccountName, gameLoginPacket.Password);

        if (account == null)
        {
            Enqueue(session, new LoginDeniedPacket(UOLoginDeniedReason.IncorrectNameOrPassword));

            return true;
        }

        session.AccountId = account.Id;
        session.AccountType = account.AccountType;

        session.NetworkSession.EnableCompression();

        var characterListPacket = new CharactersStartingLocationsPacket();
        characterListPacket.Cities.AddRange(StartingCities.AvailableStartingCities);

        var characters = await _characterService.GetCharactersForAccountAsync(session.AccountId);
        characterListPacket.FillCharacters(characters);

        Enqueue(session, new SupportFeaturesPacket());
        Enqueue(session, characterListPacket);

        return true;
    }

    private async Task<bool> HandleLoginCharacterPacketAsync(GameSession session, LoginCharacterPacket loginCharacterPacket)
    {
        var characters = await _characterService.GetCharactersForAccountAsync(session.AccountId);

        var character = characters.FirstOrDefault(c => c.Name == loginCharacterPacket.CharacterName);

        if (character == null)
        {
            _logger.Warning(
                "Character {CharacterName} not found for account {AccountId}",
                loginCharacterPacket.CharacterName,
                session.AccountId
            );

            return true;
        }

        await _gameEventBusService.PublishAsync(
            new CharacterSelectedEvent(session.SessionId, character.Id, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        );

        return true;
    }

    private Task<bool> HandleLoginSeedPacketAsync(GameSession session, LoginSeedPacket packet)
    {
        _logger.Information(
            "Received LoginSeedPacket from session {SessionId} with seed {Seed} and client version {ClientVersion}",
            session.SessionId,
            packet.Seed,
            packet.ClientVersion
        );

        return Task.FromResult(true);
    }

    private async Task<bool> HandleServerSelectPacketAsync(GameSession session, ServerSelectPacket serverSelectPacket)
    {
        var selectedIndex = serverSelectPacket.SelectedServerIndex;
        var selectedShard = _serverListPacket.Shards[selectedIndex];

        var sessionKey = new Random().Next();

        var connectToServer = new ServerRedirectPacket
        {
            IPAddress = selectedShard.IpAddress,
            Port = 2593,
            SessionKey = (uint)sessionKey
        };

        session.NetworkSession.SetSeed((uint)sessionKey);

        Enqueue(session, connectToServer);

        return true;
    }

    public async Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            Enqueue(session, SpeechMessageFactory.CreateSystem($"Welcome to {_serverConfig.Game.ShardName} !"));
            Enqueue(
                session,
                SpeechMessageFactory.CreateSystem(
                    $"Server is Moongate v{VersionUtils.Version} codename {VersionUtils.Codename}", SpeechHues.Red, 2
                )
            );
            Enqueue(session, SpeechMessageFactory.CreateSystem($"Current server time is {DateTime.UtcNow:HH:mm:ss} UTC"));
            Enqueue(
                session,
                SpeechMessageFactory.CreateSystem($"Online players: {_gameNetworkSessionService.GetAll().Count}")
            );
        }
    }
}
