using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Sessions;
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

    public GameLoginPacketHandler(RealmInstance realm, IGameHandoffStore handoffs)
    {
        _realm = realm;
        _handoffs = handoffs;
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
                    session.SetAccountId(handoff.AccountId);
                    session.SetAccountType(handoff.AccountType);
                    session.NetworkSession.SetState(NetworkSessionState.Authenticated);
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
