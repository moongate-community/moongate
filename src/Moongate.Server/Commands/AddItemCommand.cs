using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands;

/// <summary>
/// Adds a test item to the caller backpack.
/// </summary>
[RegisterConsoleCommand(
    "add_item",
    "Add an item to your backpack",
    CommandSourceType.InGame,
    AccountType.Regular
)]
public sealed class AddItemCommand : ICommandExecutor
{
    private readonly IItemService _itemService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;

    public AddItemCommand(
        IItemService itemService,
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService,
        IItemFactoryService itemFactoryService
    )
    {
        _itemService = itemService;
        _gameSessionService = gameSessionService;
        _characterService = characterService;
        _itemFactoryService = itemFactoryService;
    }

    public Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length == 0)
        {
            context.Print("Usage: add_item");

            return Task.CompletedTask;
        }

        var itemTemplateStr = context.Arguments.ElementAt(0);

        if (_itemFactoryService.TryGetItemTemplate(itemTemplateStr, out var _))
        {
            var item = _itemService.SpawnFromTemplateAsync(itemTemplateStr).GetAwaiter().GetResult();

            if (_gameSessionService.TryGet(context.SessionId, out var session))
            {
                var player = _characterService.GetCharacterAsync(session.CharacterId).GetAwaiter().GetResult();

                if (player is null)
                {
                    context.Print("Failed to add item: no active character found for your session.");

                    return Task.CompletedTask;
                }

                _itemService.MoveItemToContainerAsync(item.Id, player.BackpackId, new(1, 1), context.SessionId)
                            .GetAwaiter()
                            .GetResult();
                context.Print("Added a brick to your backpack.");
            }

            return Task.CompletedTask;
        }

        context.Print("Failed to add item: no active character found for your session.");

        return Task.CompletedTask;
    }
}
