using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Data.Internal.Interaction;

internal static class BankBoxOpenHelper
{
    public static async Task<string?> OpenAsync(
        long sessionId,
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        if (!gameSessionService.TryGet(sessionId, out var session))
        {
            return "Failed to open bank: no active session found.";
        }

        var character = await characterService.GetCharacterAsync(session.CharacterId);

        if (character is null)
        {
            return "Failed to open bank: character not found.";
        }

        var bank = await characterService.GetBankBoxWithItemsAsync(character);

        if (bank is null)
        {
            return "Failed to open bank: bank box not found.";
        }

        character.AddEquippedItem(ItemLayerType.Bank, bank);
        outgoingPacketQueue.Enqueue(session.SessionId, new DrawContainerAndAddItemCombinedPacket(bank));

        return null;
    }
}
