using System.Net;
using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Packets;
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
using Moongate.UO.Data.Version;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener,
 RegisterPacketHandler(PacketDefinition.LoginSeedPacket),
 RegisterPacketHandler(PacketDefinition.AccountLoginPacket),
 RegisterPacketHandler(PacketDefinition.ServerSelectPacket),
 RegisterPacketHandler(PacketDefinition.GameLoginPacket),
 RegisterPacketHandler(PacketDefinition.LoginCharacterPacket),
 RegisterPacketHandler(PacketDefinition.ClientTypePacket),
 RegisterPacketHandler(PacketDefinition.ClientVersionPacket)]

/// <summary>
/// Represents LoginHandler.
/// </summary>
public class LoginHandler : BasePacketListener, IGameEventListener<PlayerCharacterLoggedInEvent>
{
    private readonly ILogger _logger = Log.ForContext<LoginHandler>();

    private readonly IAccountService _accountService;
    private readonly ICharacterService _characterService;
    private readonly IGameEventBusService _gameEventBusService;

    private readonly IGameLoginHandoffService _gameLoginHandoffService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IOutboundPacketSender _outboundPacketSender;

    private readonly MoongateConfig _serverConfig;

    public LoginHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IAccountService accountService,
        ICharacterService characterService,
        IGameEventBusService gameEventBusService,
        MoongateConfig serverConfig,
        IGameLoginHandoffService gameLoginHandoffService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutboundPacketSender outboundPacketSender
    ) : base(outgoingPacketQueue)
    {
        _accountService = accountService;
        _characterService = characterService;
        _gameEventBusService = gameEventBusService;
        _serverConfig = serverConfig;
        _gameLoginHandoffService = gameLoginHandoffService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outboundPacketSender = outboundPacketSender;
    }

    public Task HandleAsync(PlayerCharacterLoggedInEvent gameEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
            {
                Enqueue(session, SpeechMessageFactory.CreateSystem($"Welcome to {_serverConfig.Game.ShardName} !"));
                Enqueue(
                    session,
                    SpeechMessageFactory.CreateSystem(
                        $"Server is Moongate v{VersionUtils.Version} codename {VersionUtils.Codename}",
                        SpeechHues.Red,
                        2
                    )
                );
                Enqueue(
                    session,
                    SpeechMessageFactory.CreateSystem($"Current server time is {DateTime.UtcNow:HH:mm:ss} UTC")
                );
                Enqueue(
                    session,
                    SpeechMessageFactory.CreateSystem($"Online players: {_gameNetworkSessionService.Count}")
                );
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
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

        if (packet is ClientTypePacket clientTypePacket)
        {
            return HandleClientTypePacketAsync(session, clientTypePacket);
        }

        if (packet is ClientVersionPacket clientVersionPacket)
        {
            return HandleClientVersionPacketAsync(session, clientVersionPacket);
        }

        return true;
    }

    private static ServerListPacket CreateServerListPacket(string shardName, IPAddress ipAddress)
    {
        var packet = new ServerListPacket();
        packet.AddShard(
            new()
            {
                Index = 0,
                IpAddress = ipAddress,
                ServerName = shardName
            }
        );

        return packet;
    }

    private async Task<bool> HandleAccountLoginPacketAsync(GameSession session, AccountLoginPacket accountLoginPacket)
    {
        _logger.Debug(
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

        Enqueue(
            session,
            CreateServerListPacket(_serverConfig.Game.ShardName, ResolveShardAddress(session))
        );

        return true;
    }

    private bool HandleClientTypePacketAsync(GameSession session, ClientTypePacket clientTypePacket)
    {
        session.NetworkSession.SetClientType(clientTypePacket.ResolvedClientType);

        if (!string.IsNullOrWhiteSpace(clientTypePacket.VersionString))
        {
            var clientVersion = new ClientVersion(clientTypePacket.VersionString);
            session.SetClientVersion(clientVersion);
            session.NetworkSession.SetClientVersion(clientVersion);
        }

        _logger.Debug(
            "Received ClientTypePacket from session {SessionId}: advertised=0x{AdvertisedClientType:X8} resolved={ClientType} version={ClientVersion}",
            session.SessionId,
            clientTypePacket.AdvertisedClientType,
            clientTypePacket.ResolvedClientType,
            string.IsNullOrWhiteSpace(clientTypePacket.VersionString) ? "<none>" : clientTypePacket.VersionString
        );

        return true;
    }

    private bool HandleClientVersionPacketAsync(GameSession session, ClientVersionPacket clientVersionPacket)
    {
        var rawVersion = clientVersionPacket.Version.TrimEnd('\0').Trim();

        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            _logger.Debug("Received empty ClientVersionPacket from session {SessionId}", session.SessionId);

            return true;
        }

        var clientVersion = new ClientVersion(rawVersion);
        session.SetClientVersion(clientVersion);
        session.NetworkSession.SetClientVersion(clientVersion);

        _logger.Debug(
            "Received ClientVersionPacket from session {SessionId}: {ClientVersion} ({ClientType})",
            session.SessionId,
            clientVersion.SourceString,
            clientVersion.Type
        );

        return true;
    }

    private async Task<bool> HandleGameLoginPacketAsync(GameSession session, GameLoginPacket gameLoginPacket)
    {
        _logger.Debug(
            "Received GameLoginPacket from session {SessionId} with account name {AccountName}",
            session.SessionId,
            gameLoginPacket.AccountName
        );

        if (_gameLoginHandoffService.TryConsume(gameLoginPacket.SessionKey, out var handoff))
        {
            session.NetworkSession.SetClientType(handoff.ClientType);

            if (handoff.ClientVersion is not null)
            {
                session.SetClientVersion(handoff.ClientVersion);
                session.NetworkSession.SetClientVersion(handoff.ClientVersion);
            }

            _logger.Debug(
                "Applied game login handoff for session {SessionId}: session key 0x{SessionKey:X8}, client type {ClientType}, version {ClientVersion}",
                session.SessionId,
                gameLoginPacket.SessionKey,
                handoff.ClientType,
                handoff.ClientVersion?.SourceString ?? "<none>"
            );
        }

        var account = await _accountService.LoginAsync(gameLoginPacket.AccountName, gameLoginPacket.Password);

        if (account == null)
        {
            Enqueue(session, new LoginDeniedPacket(UOLoginDeniedReason.IncorrectNameOrPassword));

            return true;
        }

        session.AccountId = account.Id;
        session.AccountType = account.AccountType;

        session.NetworkSession.EnableCompression();

        var characterListPacket = new CharactersStartingLocationsPacket
        {
            IsEnhancedClient = session.NetworkSession.IsEnhancedClient
        };
        characterListPacket.Cities.AddRange(StartingCities.AvailableStartingCities);

        var characters = await _characterService.GetCharactersForAccountAsync(session.AccountId);
        session.AccountCharactersCache = characters;
        characterListPacket.FillCharacters(characters);

        Enqueue(session, new SupportFeaturesPacket(GetSupportFeatureFlags(), UseExtendedSupportFeatures(session)));
        Enqueue(session, characterListPacket);

        return true;
    }

    private async Task<bool> HandleLoginCharacterPacketAsync(GameSession session, LoginCharacterPacket loginCharacterPacket)
    {
        var characters = session.AccountCharactersCache ??
                         await _characterService.GetCharactersForAccountAsync(session.AccountId);
        session.AccountCharactersCache = characters;

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
        session.NetworkSession.SetSeed(unchecked((uint)packet.Seed));
        session.NetworkSession.SetClientVersion(packet.ClientVersion);

        _logger.Debug(
            "Received LoginSeedPacket from session {SessionId} with seed {Seed} and client version {ClientVersion}",
            session.SessionId,
            packet.Seed,
            packet.ClientVersion
        );

        return Task.FromResult(true);
    }

    private async Task<bool> HandleServerSelectPacketAsync(GameSession session, ServerSelectPacket packet)
    {
        try
        {
            var sessionKey = Random.Shared.Next();
            var shardAddress = ResolveShardAddress(session);

            var connectToServer = new ServerRedirectPacket
            {
                IPAddress = shardAddress,
                Port = 2593,
                SessionKey = (uint)sessionKey
            };

            session.NetworkSession.SetSeed((uint)sessionKey);
            _gameLoginHandoffService.Store(
                connectToServer.SessionKey,
                session.NetworkSession.ClientType,
                session.NetworkSession.ClientVersion
            );

            _logger.Debug(
                "Received ServerSelectPacket from session {SessionId} with shard index {ShardIndex}; redirecting to {IPAddress}:{Port} with session key 0x{SessionKey:X8}",
                session.SessionId,
                packet.SelectedServerIndex,
                shardAddress,
                connectToServer.Port,
                connectToServer.SessionKey
            );

            var client = session.NetworkSession.Client;

            if (client is null)
            {
                _logger.Warning(
                    "Session {SessionId} has no attached client during server select redirect.",
                    session.SessionId
                );

                return true;
            }

            _ = await _outboundPacketSender.SendAsync(
                client,
                new OutgoingGamePacket(session.SessionId, connectToServer, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                CancellationToken.None
            );

            await client.CloseAsync();

            return true;
        }
        catch (Exception exception)
        {
            throw;
        }
    }

    private IPAddress ResolveShardAddress(GameSession session)
    {
        var configuredAddress = _serverConfig.Game.ServerListingAddress;

        if (!string.IsNullOrWhiteSpace(configuredAddress))
        {
            if (IPAddress.TryParse(configuredAddress, out var configuredIp))
            {
                return configuredIp;
            }

            _logger.Warning(
                "Configured server listing address '{ServerListingAddress}' is invalid. Falling back to detected local IP for session {SessionId}.",
                configuredAddress,
                session.SessionId
            );
        }

        var rawAddress = session.NetworkSession.LocalIpAddress;

        if (!string.IsNullOrWhiteSpace(rawAddress) && IPAddress.TryParse(rawAddress, out var resolved))
        {
            return resolved;
        }

        _logger.Warning(
            "Session {SessionId} has invalid LocalIpAddress '{LocalIpAddress}'. Falling back to loopback.",
            session.SessionId,
            rawAddress
        );

        return IPAddress.Loopback;
    }

    private static FeatureFlags GetSupportFeatureFlags()
        => Moongate.UO.Data.Expansions.ExpansionInfo.Table is { Length: > 0 }
            ? Moongate.UO.Data.Expansions.ExpansionInfo.CoreExpansion.SupportedFeatures
            : FeatureFlags.ExpansionEJ;

    private static bool UseExtendedSupportFeatures(GameSession session)
        => session.NetworkSession.ClientVersion?.ProtocolChanges.HasFlag(ProtocolChanges.ExtendedSupportedFeatures) ?? true;
}
