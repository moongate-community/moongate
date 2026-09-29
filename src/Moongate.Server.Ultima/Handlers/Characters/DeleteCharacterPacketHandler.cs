using Moongate.Core.Primitives;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Core.Packets;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Characters;
using Moongate.Server.Ultima.Types.Characters;
using Serilog;

namespace Moongate.Server.Ultima.Handlers.Characters;

/// <summary>
///     Marks for deletion the character the client asks to delete (0x83) and answers with the updated list (0x86), or
///     with the reason it was refused (0x85). The character stays restorable until it is removed.
/// </summary>
public sealed class DeleteCharacterPacketHandler : IAsyncPacketHandler<DeleteCharacterPacket>
{
    private readonly ILogger _logger = Log.ForContext<DeleteCharacterPacketHandler>();
    private readonly ICharacterService _characters;

    public DeleteCharacterPacketHandler(ICharacterService characters)
    {
        _characters = characters;
    }

    public async ValueTask HandleAsync(
        PacketContext context,
        DeleteCharacterPacket packet,
        CancellationToken cancellationToken
    )
    {
        var accountId = Serial.Zero;
        await context.RunOnGameLoopAsync(session => accountId = session.AccountId, cancellationToken);

        if (!accountId.IsValid)
        {
            _logger.Warning("Character deletion from session {SessionId} without an account", context.SessionId);
            await context.SendAndDisconnectAsync(
                             new CharacterDeleteResultPacket(CharacterDeleteResultType.RequestFailed),
                             cancellationToken
                         );

            return;
        }

        CharacterDeletionResult result;

        try
        {
            result = await _characters.RequestDeletionAsync(accountId, packet.CharacterIndex, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.Error(exception, "Character deletion for account {AccountId} failed", accountId);
            context.TrySend(new CharacterDeleteResultPacket(CharacterDeleteResultType.RequestFailed));

            return;
        }

        if (result.Refusal is { } refusal)
        {
            _logger.Information(
                "Character deletion for account {AccountId} at list position {Index} refused: {Refusal}",
                accountId,
                packet.CharacterIndex,
                refusal
            );
            context.TrySend(new CharacterDeleteResultPacket(refusal));

            return;
        }

        _logger.Information(
            "Session {SessionId}: account {AccountId} asked to delete {Character}",
            context.SessionId,
            accountId,
            result.Character
        );
        context.TrySend(new CharacterListUpdatePacket(result.Names));
    }
}
