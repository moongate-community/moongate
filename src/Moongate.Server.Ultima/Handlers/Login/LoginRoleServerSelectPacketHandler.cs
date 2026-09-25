using System.Security.Cryptography;
using Moongate.Network.Interfaces.Client;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

/// <summary>Issues a one-time realm ticket and redirects an authenticated login connection.</summary>
public sealed class LoginRoleServerSelectPacketHandler : ILoginPacketHandler<ServerSelectPacket>
{
    private readonly ILoginSessionService _sessions;
    private readonly IRealmCatalog _catalog;
    private readonly IGameHandoffStore _handoffs;
    private readonly ILoginPacketSendService _sender;
    private readonly ILogger _logger = Log.ForContext<LoginRoleServerSelectPacketHandler>();

    public LoginRoleServerSelectPacketHandler(
        ILoginSessionService sessions,
        IRealmCatalog catalog,
        IGameHandoffStore handoffs,
        ILoginPacketSendService sender
    )
    {
        _sessions = sessions;
        _catalog = catalog;
        _handoffs = handoffs;
        _sender = sender;
    }

    public async ValueTask HandleAsync(
        LoginSession session,
        ServerSelectPacket packet,
        CancellationToken cancellationToken
    )
    {
        if (!_sessions.IsCurrent(session) || session.NetworkSession.Client is not { } connection)
        {
            return;
        }

        if (!session.TryGetAuthenticatedAccount(
                out var accountId,
                out var accountType,
                out var username,
                out var credentialKey
            ) ||
            !accountId.IsValid)
        {
            await RejectAsync(session, connection, LoginDeniedReason.InvalidCredentials).ConfigureAwait(false);

            return;
        }

        try
        {
            RealmInstance? realm;

            try
            {
                realm = await _catalog.FindByIndexAsync(packet.ServerIndex, accountType, cancellationToken)
                                      .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Realm selection lookup failed for index {ServerIndex}", packet.ServerIndex);
                await RejectAsync(session, connection, LoginDeniedReason.CommunicationProblem).ConfigureAwait(false);

                return;
            }

            if (!IsCurrent(session, connection) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (realm is null ||
                realm.Descriptor.ServerIndex != packet.ServerIndex ||
                realm.Descriptor.MinimumAccountType > accountType)
            {
                await RejectAsync(session, connection, LoginDeniedReason.CommunicationProblem).ConfigureAwait(false);

                return;
            }

            var handoff = new PendingHandoff(
                accountId,
                accountType,
                username,
                realm.Descriptor.RealmId,
                realm.InstanceId,
                session.NetworkSession.ClientVersion
            );
            uint authKey;

            try
            {
                authKey = await _handoffs.IssueAsync(handoff, credentialKey, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.Warning(
                    exception,
                    "Realm redirect ticket issuance failed for index {ServerIndex}",
                    packet.ServerIndex
                );
                await RejectAsync(session, connection, LoginDeniedReason.CommunicationProblem).ConfigureAwait(false);

                return;
            }

            var delivered = false;

            try
            {
                if (IsCurrent(session, connection) && !cancellationToken.IsCancellationRequested)
                {
                    var redirect = new ServerRedirectPacket(realm.Descriptor.Address, realm.Descriptor.Port, authKey);

                    // The expected close cancels the login mailbox token; delivery must finish before revocation is decided.
                    delivered = await _sender.SendAndDisconnectAsync(
                                                 session.SessionId,
                                                 connection,
                                                 redirect,
                                                 CancellationToken.None
                                             )
                                             .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.Warning(exception, "Realm redirect send failed for index {ServerIndex}", packet.ServerIndex);
            }
            finally
            {
                if (!delivered)
                {
                    try
                    {
                        await _handoffs.RevokeAsync(realm.Descriptor.RealmId, authKey, CancellationToken.None)
                                       .ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(
                            exception,
                            "Realm redirect ticket revocation failed for index {ServerIndex}",
                            packet.ServerIndex
                        );
                    }
                }
            }

            if (!delivered && !cancellationToken.IsCancellationRequested)
            {
                await RejectAsync(session, connection, LoginDeniedReason.CommunicationProblem).ConfigureAwait(false);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(credentialKey);
        }
    }

    private bool IsCurrent(LoginSession session, INetworkConnection connection)
    {
        return _sessions.IsCurrent(session) && ReferenceEquals(session.NetworkSession.Client, connection);
    }

    private async Task RejectAsync(LoginSession session, INetworkConnection connection, LoginDeniedReason reason)
    {
        if (!IsCurrent(session, connection))
        {
            return;
        }

        try
        {
            if (await _sender.SendAndDisconnectAsync(
                                 session.SessionId,
                                 connection,
                                 new LoginDeniedPacket(reason),
                                 CancellationToken.None
                             )
                             .ConfigureAwait(false))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "Login denial delivery failed for session {SessionId}", session.SessionId);
        }

        await connection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
