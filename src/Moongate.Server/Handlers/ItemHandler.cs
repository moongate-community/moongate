using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Books;
using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Listeners.Base;
using Moongate.Server.Services.Items;
using Moongate.Server.Utils;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Handlers;

[
    RegisterGameEventListener,
    RegisterPacketHandler(PacketDefinition.BookHeaderNewPacket),
    RegisterPacketHandler(PacketDefinition.BookPagesPacket),
    RegisterPacketHandler(PacketDefinition.DropItemPacket),
    RegisterPacketHandler(PacketDefinition.DropWearItemPacket),
    RegisterPacketHandler(PacketDefinition.PickUpItemPacket),
    RegisterPacketHandler(PacketDefinition.SingleClickPacket),
    RegisterPacketHandler(PacketDefinition.DoubleClickPacket)
]
public class ItemHandler
    : BasePacketListener, IGameEventListener<ItemMovedEvent>, IGameEventListener<ItemAddedInSectorEvent>
      , IGameEventListener<ItemDeletedEvent>
{
    private readonly ILogger _logger = Log.ForContext<ItemHandler>();

    private readonly IItemService _itemService;

    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IGameEventBusService _gameEventBusService;
    private readonly IPlayerDragService _playerDragService;
    private readonly IItemScriptDispatcher? _itemScriptDispatcher;
    private readonly ICharacterService? _characterService;
    private readonly IItemBookService _itemBookService;
    private readonly IItemInteractionService _itemInteractionService;
    private readonly IItemManipulationService _itemManipulationService;

    private readonly ISpatialWorldService _spatialWorldService;

    public ItemHandler(
        IOutgoingPacketQueue outgoingPacketQueue,
        IItemService itemService,
        IGameEventBusService gameEventBusService,
        IGameNetworkSessionService gameNetworkSessionService,
        IPlayerDragService playerDragService,
        ISpatialWorldService spatialWorldService,
        IMobileService mobileService,
        IItemScriptDispatcher? itemScriptDispatcher = null,
        ICharacterService? characterService = null,
        IItemBookService? itemBookService = null,
        IItemInteractionService? itemInteractionService = null,
        IItemManipulationService? itemManipulationService = null
    ) : base(outgoingPacketQueue)
    {
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _playerDragService = playerDragService;
        _spatialWorldService = spatialWorldService;
        _mobileService = mobileService;
        _itemBookService = itemBookService ?? new ItemBookService(itemService, mobileService, outgoingPacketQueue);
        _itemInteractionService = itemInteractionService
                                  ?? new ItemInteractionService(
                                      itemService,
                                      gameEventBusService,
                                      _itemBookService,
                                      outgoingPacketQueue,
                                      itemScriptDispatcher,
                                      characterService
                                  );
        _itemManipulationService = itemManipulationService
                                   ?? new ItemManipulationService(
                                       itemService,
                                       gameEventBusService,
                                       playerDragService,
                                       spatialWorldService,
                                       mobileService,
                                       gameNetworkSessionService,
                                       outgoingPacketQueue
                                   );
        _itemScriptDispatcher = itemScriptDispatcher;
        _characterService = characterService;
        {
            _gameEventBusService = gameEventBusService;
        }
    }

    public async Task HandleAsync(ItemMovedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        if (gameEvent.NewContainerId == Serial.Zero)
        {
            return;
        }

        if (_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            var container = await _itemService.GetItemAsync(gameEvent.NewContainerId);

            if (container is null)
            {
                return;
            }

            Enqueue(session, new DrawContainerAndAddItemCombinedPacket(container));
        }
    }

    public async Task HandleAsync(ItemAddedInSectorEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var item = await _itemService.GetItemAsync(gameEvent.ItemId);

        if (item is null)
        {
            return;
        }

        var range = _spatialWorldService.GetUpdateBroadcastSectorRadius() * MapSectorConsts.SectorSize;
        var sessions = _spatialWorldService.GetPlayersInRange(item.Location, range, gameEvent.MapId);

        foreach (var session in sessions)
        {
            if (!ItemVisibilityHelper.CanSessionSeeItem(session, item))
            {
                continue;
            }

            Enqueue(session, ItemPacketHelper.CreateObjectInformationPacket(item, session));
        }
    }

    public async Task HandleAsync(ItemDeletedEvent gameEvent, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (gameEvent.OldContainerId == Serial.Zero)
        {
            return;
        }

        // Fast path: check if source session owns the container (personal backpack)
        if (gameEvent.SessionId > 0 && _gameNetworkSessionService.TryGet(gameEvent.SessionId, out var sourceSession))
        {
            var sourceCharacter = sourceSession.Character;

            if (sourceCharacter is not null &&
                (sourceCharacter.BackpackId == gameEvent.OldContainerId ||
                 sourceCharacter.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var sourceBackpackId) &&
                 sourceBackpackId == gameEvent.OldContainerId))
            {
                var container = await _itemService.GetItemAsync(gameEvent.OldContainerId);

                if (container is not null)
                {
                    Enqueue(sourceSession.SessionId, new DrawContainerAndAddItemCombinedPacket(container));
                }

                return;
            }
        }

        // Slow path: shared container — scan all sessions
        var targetSessionIds = new HashSet<long>();

        if (gameEvent.SessionId > 0 && _gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            targetSessionIds.Add(session.SessionId);
        }

        foreach (var otherSession in _gameNetworkSessionService.GetAll())
        {
            var character = otherSession.Character;

            if (character is null)
            {
                continue;
            }

            if (character.BackpackId == gameEvent.OldContainerId ||
                character.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId) &&
                equippedBackpackId == gameEvent.OldContainerId)
            {
                targetSessionIds.Add(otherSession.SessionId);
            }
        }

        if (targetSessionIds.Count == 0)
        {
            return;
        }

        var sharedContainer = await _itemService.GetItemAsync(gameEvent.OldContainerId);

        if (sharedContainer is null)
        {
            return;
        }

        foreach (var sessionId in targetSessionIds)
        {
            Enqueue(sessionId, new DrawContainerAndAddItemCombinedPacket(sharedContainer));
        }
    }

    protected override async Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is DropItemPacket dropItemPacket)
        {
            return await _itemManipulationService.HandleDropItemAsync(session, dropItemPacket);
        }

        if (packet is PickUpItemPacket pickUpItemPacket)
        {
            return await _itemManipulationService.HandlePickUpItemAsync(session, pickUpItemPacket);
        }

        if (packet is DropWearItemPacket dropWearItemPacket)
        {
            return await _itemManipulationService.HandleDropWearItemAsync(session, dropWearItemPacket);
        }

        if (packet is BookPagesPacket bookPagesPacket)
        {
            return await _itemBookService.HandleBookPagesAsync(session, bookPagesPacket);
        }

        if (packet is BookHeaderNewPacket bookHeaderNewPacket)
        {
            return await _itemBookService.HandleBookHeaderAsync(session, bookHeaderNewPacket);
        }

        if (packet is SingleClickPacket singleClickPacket)
        {
            return await _itemInteractionService.HandleSingleClickAsync(session, singleClickPacket);
        }

        if (packet is DoubleClickPacket doubleClickPacket)
        {
            return await _itemInteractionService.HandleDoubleClickAsync(session, doubleClickPacket);
        }

        return true;
    }

}
