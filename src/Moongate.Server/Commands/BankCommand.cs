using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Opens the bank container for the current player.
/// </summary>
[RegisterConsoleCommand("bank", "Open your bank box.", CommandSourceType.InGame, AccountType.Regular)]
public sealed class BankCommand : ICommandExecutor
{
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;
    private readonly IItemService _itemService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public BankCommand(
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService,
        IItemService itemService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _gameSessionService = gameSessionService;
        _characterService = characterService;
        _itemService = itemService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (!_gameSessionService.TryGet(context.SessionId, out var session))
        {
            context.Print("Failed to open bank: no active session found.");

            return;
        }

        var character = await _characterService.GetCharacterAsync(session.CharacterId);

        if (character is null)
        {
            context.Print("Failed to open bank: character not found.");

            return;
        }

        if (!character.EquippedItemIds.TryGetValue(ItemLayerType.Bank, out var bankId) || bankId == Serial.Zero)
        {
            context.Print("Failed to open bank: no bank box equipped.");

            return;
        }

        var bank = await _itemService.GetItemAsync(bankId);

        if (bank is null)
        {
            context.Print("Failed to open bank: bank box not found.");

            return;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, new DrawContainerAndAddItemCombinedPacket(bank));
        context.Print("Bank box opened.");
    }
}
