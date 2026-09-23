using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Login;

public sealed class AccountLoginPacketHandler : IAsyncPacketHandler<AccountLoginPacket>
{
    private readonly IPacketSendService _sender;
    private readonly ILogger _logger = Log.ForContext<AccountLoginPacketHandler>();

    private readonly IAccountService _accountService;

    public AccountLoginPacketHandler(IPacketSendService sender, IAccountService accountService)
    {
        _sender = sender;
        _accountService = accountService;
    }

    public async ValueTask HandleAsync(PacketContext context, AccountLoginPacket packet, CancellationToken cancellationToken)
    {
        var account = await _accountService.LoginAsync(packet.Account, packet.Password, cancellationToken);

        await context.RunOnGameLoopAsync(
            (session) =>
            {
                if (account == null)
                {
                    _logger.Information("Login failed for account {Account}", packet.Account);

                    if (!_sender.TrySend(session.SessionId, new LoginDeniedPacket(LoginDeniedReason.InvalidCredentials)))
                    {
                        _ = _sender.DisconnectAsync(session.SessionId);
                    }

                    return;
                }

                session.SetAccountId(account.Id);
                session.SetAccountType(account.AccountType);

                _logger.Information(
                    "Login successful for account {Account} (Level: {AccountLevel})",
                    packet.Account,
                    account.AccountType
                );
            },
            cancellationToken
        );
    }
}
