using Moongate.Server.Attributes;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Containers;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "gm_body|.gm_body",
    "Create GM body bag and add GM tools. Usage: .gm_body",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class GmBodyCommand : ICommandExecutor
{
    private const string BagTemplateId = "gm_body_bag";

    private readonly IItemService _itemService;
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;
    private readonly IItemFactoryService _itemFactoryService;

    public GmBodyCommand(
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

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 0)
        {
            context.Print("Usage: .gm_body");

            return;
        }

        if (!_gameSessionService.TryGet(context.SessionId, out var session))
        {
            context.Print("Failed to setup GM body: no active session found.");

            return;
        }

        var character = await _characterService.GetCharacterAsync(session.CharacterId);

        if (character is null)
        {
            context.Print("Failed to setup GM body: character not found.");

            return;
        }

        var backpackId = ResolveBackpackId(character);

        if (backpackId == Serial.Zero)
        {
            context.Print("Failed to setup GM body: backpack not found.");

            return;
        }

        try
        {
            if (!_itemFactoryService.TryGetItemTemplate(BagTemplateId, out var bagTemplate) || bagTemplate is null)
            {
                context.Print("Failed to setup GM body: template '{0}' not found.", BagTemplateId);

                return;
            }

            var bag = await _itemService.SpawnFromTemplateAsync(BagTemplateId);

            if (!await _itemService.MoveItemToContainerAsync(bag.Id, backpackId, new Point2D(1, 1), context.SessionId))
            {
                context.Print("Failed to setup GM body: could not add GM bag to backpack.");

                return;
            }

            var contentTemplateIds = bagTemplate.Container
                                                .Where(static templateId => !string.IsNullOrWhiteSpace(templateId))
                                                .Select(static templateId => templateId.Trim())
                                                .ToList();

            for (var index = 0; index < contentTemplateIds.Count; index++)
            {
                var position = ContainerLayout.GetGridPosition(index);
                await AddTemplateItemToContainerAsync(contentTemplateIds[index], bag.Id, position, context.SessionId);
            }

            context.Print("GM body kit added successfully.");
        }
        catch (Exception)
        {
            context.Print("Failed to setup GM body: missing or invalid GM templates.");
        }
    }

    private async Task AddTemplateItemToContainerAsync(
        string templateId,
        Serial containerId,
        Point2D position,
        long sessionId
    )
    {
        var item = await _itemService.SpawnFromTemplateAsync(templateId);
        _ = await _itemService.MoveItemToContainerAsync(item.Id, containerId, position, sessionId);
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
