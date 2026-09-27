using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Types.Login;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     What both create-character handlers do once they have a request: read the session's account, create the
///     character, and answer a refusal with a popup and a disconnect. A created character gets no reply yet: entering
///     the world comes later.
/// </summary>
internal static class CharacterCreationReply
{
    public static async ValueTask HandleAsync(
        PacketContext context,
        ICharacterService characters,
        CharacterCreationRequest request,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var accountId = Serial.Zero;
        await context.RunOnGameLoopAsync(session => accountId = session.AccountId, cancellationToken)
                     .ConfigureAwait(false);

        if (!accountId.IsValid)
        {
            logger.Warning("Character creation from session {SessionId} without an account", context.SessionId);
            await context.SendAndDisconnectAsync(new PopupMessagePacket(PopupMessageType.CouldNotAttach), cancellationToken)
                         .ConfigureAwait(false);

            return;
        }

        var result = await characters.CreateAsync(accountId, request, cancellationToken).ConfigureAwait(false);

        if (result.Refusal is { } refusal)
        {
            logger.Information(
                "Character creation for account {AccountId} refused: {Refusal}",
                accountId,
                refusal
            );
            await context.SendAndDisconnectAsync(new PopupMessagePacket(ToPopup(refusal)), cancellationToken)
                         .ConfigureAwait(false);
        }
    }

    private static PopupMessageType ToPopup(CharacterCreationRefusalType refusal)
    {
        return refusal switch
        {
            // As RunUO: a full account is reported as a sync error, which sends the client back to the login.
            CharacterCreationRefusalType.TooManyCharacters => PopupMessageType.LoginSyncError,
            _ => PopupMessageType.CharacterExists
        };
    }
}
