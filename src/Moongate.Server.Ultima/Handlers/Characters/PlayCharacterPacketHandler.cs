using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     Loads the character the client chose (0x5D) and brings it into the world through
///     <see cref="ICharacterEnterWorldService" />.
/// </summary>
public sealed class PlayCharacterPacketHandler : IAsyncPacketHandler<PlayCharacterPacket>
{
    private readonly ILogger _logger = Log.ForContext<PlayCharacterPacketHandler>();
    private readonly ICharacterService _characters;
    private readonly ICharacterLeaveWorldService _leaves;
    private readonly ICharacterEnterWorldService _enter;

    public PlayCharacterPacketHandler(
        ICharacterService characters,
        ICharacterLeaveWorldService leaves,
        ICharacterEnterWorldService enter
    )
    {
        _characters = characters;
        _leaves = leaves;
        _enter = enter;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        PlayCharacterPacket packet,
        CancellationToken cancellationToken
    )
    {
        var accountId = Serial.Zero;
        await context.RunOnGameLoopAsync(session => accountId = session.AccountId, cancellationToken);

        if (!accountId.IsValid)
        {
            _logger.Warning("Play character from session {SessionId} without an account", context.SessionId);
            await RefuseAsync(context, PopupMessageType.CouldNotAttach, cancellationToken);

            return;
        }

        CharacterForPlay? play;

        try
        {
            // A save of this account's last session may still be running: read what it writes, never the rows before it.
            await _leaves.WaitForAccountAsync(accountId);
            play = await _characters.GetForPlayAsync(accountId, packet.CharacterIndex, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error(exception, "Loading the character to play for account {AccountId} failed", accountId);
            await RefuseAsync(context, PopupMessageType.CouldNotAttach, cancellationToken);

            return;
        }

        if (play is null)
        {
            _logger.Information(
                "Account {AccountId} chose list position {Index}, which holds no character",
                accountId,
                packet.CharacterIndex
            );
            await RefuseAsync(context, PopupMessageType.CharacterDoesNotExist, cancellationToken);

            return;
        }

        await _enter.EnterAsync(context, accountId, play, cancellationToken);
    }

    private static async Task RefuseAsync(PacketContext context, PopupMessageType popup, CancellationToken cancellationToken)
    {
        await context.SendAndDisconnectAsync(new PopupMessagePacket(popup), cancellationToken);
    }
}
