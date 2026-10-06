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
///     character, and answer a refusal with a popup and a disconnect. A created character enters the world at once,
///     as in the other emulators.
/// </summary>
internal static class CharacterCreationReply
{
    public static async ValueTask HandleAsync(
        PacketContext context,
        ICharacterService characters,
        ICharacterEnterWorldService enter,
        CharacterCreationRequest request,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var accountId = Serial.Zero;
        var canEnter = false;
        await context.RunOnGameLoopAsync(
            session =>
            {
                accountId = session.AccountId;
                canEnter = enter.CanEnter(session);
            },
            cancellationToken
        );

        if (!accountId.IsValid)
        {
            logger.Warning("Character creation from session {SessionId} without an account", context.SessionId);
            await context.SendAndDisconnectAsync(new PopupMessagePacket(PopupMessageType.CouldNotAttach), cancellationToken);

            return;
        }

        if (!canEnter)
        {
            // Before the save: a client that cannot enter the world must not use up a slot.
            logger.Information("Account {AccountId} already has a character in the world", accountId);
            await context.SendAndDisconnectAsync(
                new PopupMessagePacket(PopupMessageType.CharacterInWorld),
                cancellationToken
            );

            return;
        }

        CharacterCreationResult result;

        try
        {
            result = await characters.CreateAsync(accountId, request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Without an answer the client waits on its spinner until it times out; tell it and close instead.
            logger.Error(exception, "Character creation for account {AccountId} failed", accountId);
            await context.SendAndDisconnectAsync(new PopupMessagePacket(PopupMessageType.CouldNotAttach), cancellationToken);

            return;
        }

        if (result.Refusal is { } refusal)
        {
            logger.Information(
                "Character creation for account {AccountId} refused: {Refusal}",
                accountId,
                refusal
            );
            await context.SendAndDisconnectAsync(new PopupMessagePacket(ToPopup(refusal)), cancellationToken);

            return;
        }

        var character = result.Character!;
        logger.Information(
            "Session {SessionId}: account {AccountId} created character {Serial} {Name} ({Race} {Gender}) in slot {Slot} at {Map} {Location}",
            context.SessionId,
            accountId,
            character.Id,
            character.Name,
            character.Race,
            character.Gender,
            character.Slot,
            character.Map,
            character.Location
        );

        // The starting items the character wears, and everything inside them; every starting item is one or the other.
        var equipment = result.Items.Where(item => item.MobileId == character.Id).ToList();
        var contents = result.Items.Where(item => item.MobileId != character.Id).ToList();
        await enter.EnterAsync(context, accountId, new(character, equipment, contents), cancellationToken);
    }

    private static PopupMessageType ToPopup(CharacterCreationRefusalType refusal)
    {
        return refusal switch
        {
            // As RunUO: a full account is reported as a sync error, which sends the client back to the login.
            CharacterCreationRefusalType.TooManyCharacters => PopupMessageType.LoginSyncError,
            _                                              => PopupMessageType.CharacterExists
        };
    }
}
