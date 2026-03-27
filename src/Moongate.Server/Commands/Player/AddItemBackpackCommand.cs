using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

/// <summary>
/// Spawns an item template and places it in the current player backpack.
/// </summary>
[RegisterConsoleCommand(
    "add_item_backpack",
    "Add an item template to your backpack. Usage: .add_item_backpack <templateId> [amount]",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class AddItemBackpackCommand : ICommandExecutor
{
    private readonly IItemService _itemService;
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;

    public AddItemBackpackCommand(
        IItemService itemService,
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService
    )
    {
        _itemService = itemService;
        _gameSessionService = gameSessionService;
        _characterService = characterService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (
            context.Arguments.Length != 1 && context.Arguments.Length != 2 || string.IsNullOrWhiteSpace(context.Arguments[0])
        )
        {
            context.Print("Usage: .add_item_backpack <templateId> [amount]");

            return;
        }

        var amount = 1;

        if (
            context.Arguments.Length == 2 && (!int.TryParse(context.Arguments[1], out amount) || amount <= 0)
        )
        {
            context.Print("Usage: .add_item_backpack <templateId> [amount]");

            return;
        }

        if (!_gameSessionService.TryGet(context.SessionId, out var session))
        {
            context.Print("Failed to add item: no active session found.");

            return;
        }

        var character = await _characterService.GetCharacterAsync(session.CharacterId);

        if (character is null)
        {
            context.Print("Failed to add item: character not found.");

            return;
        }

        var backpackId = ResolveBackpackId(character);

        if (backpackId == Serial.Zero)
        {
            context.Print("Failed to add item: backpack not found.");

            return;
        }

        var templateId = context.Arguments[0].Trim();

        try
        {
            var item = await _itemService.SpawnFromTemplateAsync(templateId);

            if (amount > 1)
            {
                if (!item.IsStackable)
                {
                    context.Print("Failed to add item: template '{0}' is not stackable.", templateId);

                    return;
                }

                item.Amount = amount;
                await _itemService.UpsertItemAsync(item);
            }

            var moved = await _itemService.MoveItemToContainerAsync(item.Id, backpackId, new(1, 1), context.SessionId);

            if (!moved)
            {
                context.Print("Failed to add item: could not move item to backpack.");

                return;
            }

            if (amount > 1)
            {
                context.Print("Added '{0}' x{1} to backpack.", templateId, amount);

                return;
            }

            context.Print("Added '{0}' to backpack.", templateId);
        }
        catch (Exception)
        {
            context.Print("Failed to add item: template '{0}' not found or invalid.", templateId);
        }
    }

    private static Serial ResolveBackpackId(UOMobileEntity character)
    {
        if (character.BackpackId != Serial.Zero)
        {
            return character.BackpackId;
        }

        if (character.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId))
        {
            return equippedBackpackId;
        }

        return Serial.Zero;
    }
}
