using Moongate.Network.Packets.Types.Targeting;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Targeting;
using Moongate.Server.Data.Internal.Commands;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Console;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Movement;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Services.World;
using Moongate.Server.Types.Commands;
using Moongate.Server.Utils;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Commands.Player;

[RegisterConsoleCommand(
    "add_door|.add_door",
    "Add a targeted door. Usage: .add_door [wood|metal]",
    CommandSourceType.InGame,
    AccountType.GameMaster
)]
public sealed class AddDoorCommand : ICommandExecutor
{
    private const string WoodDoorType = "wood";
    private const string MetalDoorType = "metal";
    private const string WoodDoorTemplateId = "light_wood_door";
    private const string MetalDoorTemplateId = "metal_door";
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameNetworkSessionService _gameSessionService;
    private readonly ICharacterService _characterService;
    private readonly IMovementTileQueryService _movementTileQueryService;

    public AddDoorCommand(
        IGameEventBusService gameEventBusService,
        IItemFactoryService itemFactoryService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService,
        IGameNetworkSessionService gameSessionService,
        ICharacterService characterService,
        IMovementTileQueryService movementTileQueryService
    )
    {
        _gameEventBusService = gameEventBusService;
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _gameSessionService = gameSessionService;
        _characterService = characterService;
        _movementTileQueryService = movementTileQueryService;
    }

    public async Task ExecuteCommandAsync(CommandSystemContext context)
    {
        if (!TryParseDoorType(context.Arguments, out var doorType))
        {
            context.Print("Usage: .add_door [wood|metal]");

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
                            context.PrintError("Cannot add door: active in-game session not found.");

                            return;
                        }

                        var location = callback.Packet.Location;
                        var item = _itemFactoryService.CreateItemFromTemplate(GetTemplateId(doorType));
                        var facing = DoorPlacementUtils.ResolveFacing(
                            _movementTileQueryService,
                            _spatialWorldService,
                            mapId,
                            location
                        );

                        item.MapId = mapId;
                        item.Location = location;
                        item.Direction = facing.ToDirectionType();
                        item.ItemId = facing.ToItemId(item.ItemId);
                        item.SetCustomString(ItemCustomParamKeys.Door.Facing, facing.ToString());

                        _itemService.CreateItemAsync(item).GetAwaiter().GetResult();
                        _spatialWorldService.AddOrUpdateItem(item, mapId);

                        context.Print(
                            "Door '{0}' spawned at {1} (Map={2}, Facing={3}, Serial={4}).",
                            doorType,
                            location,
                            mapId,
                            facing,
                            item.Id
                        );
                    }
                    catch (Exception ex)
                    {
                        context.PrintError("Failed to add door: {0}", ex.Message);
                    }
                }
            )
        );
    }

    private static string GetTemplateId(string doorType)
        => string.Equals(doorType, MetalDoorType, StringComparison.OrdinalIgnoreCase)
               ? MetalDoorTemplateId
               : WoodDoorTemplateId;

    private static bool TryParseDoorType(string[] arguments, out string doorType)
    {
        doorType = WoodDoorType;

        if (arguments.Length == 0)
        {
            return true;
        }

        if (arguments.Length != 1)
        {
            return false;
        }

        var normalizedDoorType = arguments[0].Trim();

        if (string.Equals(normalizedDoorType, WoodDoorType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalizedDoorType, MetalDoorType, StringComparison.OrdinalIgnoreCase))
        {
            doorType = MetalDoorType;

            return true;
        }

        return false;
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
