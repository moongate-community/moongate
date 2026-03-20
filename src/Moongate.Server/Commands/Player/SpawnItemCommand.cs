using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

/// <summary>
/// Spawns an item template at a targeted world location.
/// </summary>
[RegisterConsoleCommand(
    "spawn_item|.spawn_item",
    "Spawn an item template at target location. Usage: .spawn_item <templateId>",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class SpawnItemCommand : ICommandExecutor
{
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;

    public SpawnItemCommand(
        IGameEventBusService gameEventBusService,
        IItemFactoryService itemFactoryService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService,
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService
    )
    {
        _gameEventBusService = gameEventBusService;
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _gameSessionService = gameSessionService;
        _characterService = characterService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (context.Arguments.Length != 1 || string.IsNullOrWhiteSpace(context.Arguments[0]))
        {
            context.Print("Usage: .spawn_item <templateId>");

            return;
        }

        var templateId = context.Arguments[0].Trim();

        if (!_itemFactoryService.TryGetItemTemplate(templateId, out _))
        {
            context.PrintError("Unknown item template: {0}", templateId);

            return;
        }

        await _gameEventBusService.PublishAsync(
            new TargetRequestCursorEvent(
                context.SessionId,
                TargetCursorSelectionType.SelectLocation,
                TargetCursorType.Helpful,
                callback =>
                {
                    try
                    {
                        if (!TryResolveMapId(context.SessionId, out var mapId))
                        {
                            context.PrintError("Cannot spawn item: active in-game session not found.");

                            return;
                        }

                        var item = _itemFactoryService.CreateItemFromTemplate(templateId);
                        item.MapId = mapId;
                        item.Location = callback.Packet.Location;

                        _itemService.CreateItemAsync(item).GetAwaiter().GetResult();
                        _spatialWorldService.AddOrUpdateItem(item, mapId);

                        context.Print(
                            "Item '{0}' spawned at {1} (Map={2}, Serial={3}).",
                            templateId,
                            callback.Packet.Location,
                            mapId,
                            item.Id
                        );
                    }
                    catch (Exception ex)
                    {
                        context.PrintError("Failed to spawn item '{0}': {1}", templateId, ex.Message);
                    }
                }
            )
        );
    }

    private bool TryResolveMapId(long sessionId, out int mapId)
    {
        mapId = 0;

        if (!_gameSessionService.TryGet(sessionId, out var session))
        {
            return false;
        }

        mapId = session.Character?.MapId ??
                _characterService.GetCharacterAsync(session.CharacterId)
                                 .GetAwaiter()
                                 .GetResult()
                                 ?.MapId ??
                1;

        return true;
    }
}
