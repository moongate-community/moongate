using Moongate.Core.Types.Expansions;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Sessions;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Maps;
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


    public GameLoginPacketHandler(RealmInstance realm, IGameHandoffStore handoffs, IDataLoaderService dataLoaderService)
    {
        _realm = realm;
        _handoffs = handoffs;
        _dataLoaderService = dataLoaderService;
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

                    // From here on the client expects everything the game server sends to be Huffman-compressed.
                    session.NetworkSession.EnableCompression();
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        await context.RunOnGameLoopAsync(
            session =>
            {

                var characterListPacket = new CharacterListPacket(
                    [null, null, null,null, null, null, null],
                    _dataLoaderService.GetEntities<StartingCityContent>(),
                    CharacterListFlags.Default |
                    CharacterListFlags.SixthCharacterSlot |
                    CharacterListFlags.SeventhCharacterSlot
                );

                context.TrySend(new SupportFeaturesPacket(FeatureFlags.ExpansionEj | FeatureFlags.SeventhCharacterSlot));
                context.TrySend(characterListPacket);


            }, cancellationToken);
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
