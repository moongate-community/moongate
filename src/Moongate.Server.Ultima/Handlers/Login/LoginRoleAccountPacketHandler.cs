using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Services;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

/// <summary>Authenticates on the login pipeline and sends one current realm snapshot.</summary>
public sealed class LoginRoleAccountPacketHandler : ILoginPacketHandler<AccountLoginPacket>
{
    private readonly ILoginSessionService _sessions;
    private readonly IPacketSendService _sender;
    private readonly LoginAccountFlow _flow;
    private readonly ILogger _logger = Log.ForContext<LoginRoleAccountPacketHandler>();

    public LoginRoleAccountPacketHandler(ILoginSessionService sessions, IPacketSendService sender,
        LoginAccountFlow flow)
    {
        _sessions = sessions;
        _sender = sender;
        _flow = flow;
    }

    public async ValueTask HandleAsync(LoginSession session, AccountLoginPacket packet,
        CancellationToken cancellationToken)
    {
        var result = await _flow.AuthenticateAsync(packet.Account, packet.Password, cancellationToken)
            .ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested || !_sessions.IsCurrent(session) ||
            session.NetworkSession.Client is not { } connection)
        {
            return;
        }

        if (!result.Success)
        {
            _logger.Information("Login failed for account {Account}: {Reason}", packet.Account, result.DenialReason);
            if (!_sender.TrySend(session.SessionId, connection, new LoginDeniedPacket(result.DenialReason!.Value)))
            {
                await connection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return;
        }

        if (!session.TrySetAccount(result.AccountId, result.AccountType))
        {
            return;
        }

        if (!_sessions.IsCurrent(session))
        {
            session.ClearAccount();
            return;
        }

        if (!_sender.TrySend(session.SessionId, connection, new ServerListPacket(result.Servers)))
        {
            session.ClearAccount();
            await connection.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        _logger.Information("Login successful for account {Account}; {RealmCount} realms available",
            packet.Account, result.Servers.Count);
    }
}
