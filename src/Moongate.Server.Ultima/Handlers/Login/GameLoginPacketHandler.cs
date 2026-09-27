using Moongate.Core.Types.Expansions;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Config;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Sessions;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Maps;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Types.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

/// <summary>
///     Redeems a one-time login redirect before associating a game session with its account.
/// </summary>
public sealed class GameLoginPacketHandler : IAsyncPacketHandler<GameLoginPacket>
{
    private readonly RealmInstance _realm;
    private readonly IGameHandoffStore _handoffs;
    private readonly ILogger _logger = Log.ForContext<GameLoginPacketHandler>();

    private readonly IDataLoaderService _dataLoaderService;
    private readonly ICharacterService _characters;
    private readonly CharactersConfig _charactersConfig;

    public GameLoginPacketHandler(
        RealmInstance realm,
        IGameHandoffStore handoffs,
        IDataLoaderService dataLoaderService,
        ICharacterService characters,
        CharactersConfig charactersConfig
    )
    {
        _realm = realm;
        _handoffs = handoffs;
        _dataLoaderService = dataLoaderService;
        _characters = characters;
        _charactersConfig = charactersConfig;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        GameLoginPacket packet,
        CancellationToken cancellationToken
    )
    {
        if (packet.AuthKey == 0 || context.Seed != packet.AuthKey)
        {
            await DenyAsync(context, cancellationToken).ConfigureAwait(false);

            return;
        }

        PendingHandoff? handoff;

        try
        {
            handoff = await _handoffs.RedeemAsync(
                    _realm.Descriptor.RealmId,
                    _realm.InstanceId,
                    packet.AuthKey,
                    packet.Account,
                    packet.Password,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.Warning("Game handoff lookup failed: {FailureType}", exception.GetType().Name);
            await DenyAsync(context, cancellationToken).ConfigureAwait(false);

            return;
        }

        if (handoff is null ||
            !handoff.AccountId.IsValid ||
            !StringComparer.Ordinal.Equals(handoff.RealmId, _realm.Descriptor.RealmId) ||
            handoff.InstanceId != _realm.InstanceId ||
            !StringComparer.Ordinal.Equals(handoff.Username, packet.Account))
        {
            await DenyAsync(context, cancellationToken).ConfigureAwait(false);

            return;
        }

        await context.RunOnGameLoopAsync(
                session =>
                {
                    session.Set(SessionKeys.AccountId, handoff.AccountId);
                    session.Set(SessionKeys.AccountType, handoff.AccountType);
                    session.NetworkSession.SetState(NetworkSessionState.Authenticated);

                    // The client reported its version to the login server only; the handoff carries it over.
                    if (handoff.ClientVersion is { } clientVersion)
                    {
                        session.NetworkSession.SetClientVersion(clientVersion);
                    }

                    // From here on the client expects everything the game server sends to be Huffman-compressed.
                    session.NetworkSession.EnableCompression();
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        IReadOnlyList<MobileEntity> characters;

        try
        {
            characters = await _characters.GetCharactersAsync(handoff.AccountId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            // The client is already authenticated and waits for its character list; deny rather than leave it hanging.
            _logger.Error(exception, "Character list lookup for account {AccountId} failed", handoff.AccountId);
            await DenyAsync(context, cancellationToken).ConfigureAwait(false);

            return;
        }
        var maxPerAccount = _charactersConfig.MaxPerAccount;
        var characterListPacket = new CharacterListPacket(
            CharacterListBuilder.Names(characters, maxPerAccount),
            _dataLoaderService.GetEntities<StartingCityContent>(),
            CharacterListFlags.Default | CharacterListBuilder.SlotFlags(maxPerAccount)
        );

        await context.RunOnGameLoopAsync(
                _ =>
                {
                    context.TrySend(new SupportFeaturesPacket(CharacterListBuilder.Features(maxPerAccount)));
                    context.TrySend(characterListPacket);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private static async Task DenyAsync(PacketContext context, CancellationToken cancellationToken)
    {
        await context.SendAndDisconnectAsync(
                new LoginDeniedPacket(LoginDeniedReason.CommunicationProblem),
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
