using Moongate.Core.Primitives;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Services;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class AccountLoginPacketHandler : IAsyncPacketHandler<AccountLoginPacket>
{
    private readonly ILogger _logger = Log.ForContext<AccountLoginPacketHandler>();

    private readonly LoginAccountFlow _flow;

    public AccountLoginPacketHandler(LoginAccountFlow flow)
    {
        _flow = flow;
    }

    public async ValueTask HandleAsync(PacketContext context, AccountLoginPacket packet, CancellationToken cancellationToken)
    {
        var result = await _flow.AuthenticateAsync(packet.Account, packet.Password, cancellationToken);

        await context.RunOnGameLoopAsync(
            session =>
            {
                if (!result.Success)
                {
                    _logger.Information("Login failed for account {Account}", packet.Account);

                    if (!context.TrySend(new LoginDeniedPacket(result.DenialReason!.Value)))
                    {
                        _ = session.NetworkSession.Client?.CloseAsync(cancellationToken);
                    }

                    return;
                }

                session.Set(SessionKeys.AccountId, result.AccountId);
                session.Set(SessionKeys.AccountType, result.AccountType);

                if (!context.TrySend(new ServerListPacket(result.Servers)))
                {
                    session.Set(SessionKeys.AccountId, Serial.Zero);
                    session.Set(SessionKeys.AccountType, AccountType.Regular);
                    _ = session.NetworkSession.Client?.CloseAsync(cancellationToken);

                    return;
                }

                _logger.Information(
                    "Login successful for account {Account} (Level: {AccountLevel})",
                    packet.Account,
                    result.AccountType
                );
            },
            cancellationToken
        );
    }
}
