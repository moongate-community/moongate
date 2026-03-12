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
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Listeners.Base;
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
    RegisterPacketHandler(PacketDefinition.BookHeaderOldPacket),
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
    private const int GroundItemInteractionRange = 2;
    private const int BookLinesPerPage = 8;

    private readonly ILogger _logger = Log.ForContext<ItemHandler>();

    private readonly IItemService _itemService;

    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    private readonly IGameEventBusService _gameEventBusService;
    private readonly IPlayerDragService _playerDragService;
    private readonly IItemScriptDispatcher? _itemScriptDispatcher;
    private readonly ICharacterService? _characterService;

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
        ICharacterService? characterService = null
    ) : base(outgoingPacketQueue)
    {
        _itemService = itemService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _playerDragService = playerDragService;
        _spatialWorldService = spatialWorldService;
        _mobileService = mobileService;
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

            Enqueue(session, new ObjectInformationPacket(item));
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
            return await HandleDropItemAsync(session, dropItemPacket);
        }

        if (packet is PickUpItemPacket pickUpItemPacket)
        {
            return await HandlePickUpItemAsync(session, pickUpItemPacket);
        }

        if (packet is DropWearItemPacket dropWearItemPacket)
        {
            return await HandleDropWearItemAsync(session, dropWearItemPacket);
        }

        if (packet is BookPagesPacket bookPagesPacket)
        {
            return await HandleBookPagesAsync(session, bookPagesPacket);
        }

        if (packet is BookHeaderOldPacket bookHeaderOldPacket)
        {
            return await HandleBookHeaderAsync(session, bookHeaderOldPacket);
        }

        if (packet is SingleClickPacket singleClickPacket)
        {
            return await HandleSingleClickAsync(session, singleClickPacket);
        }

        if (packet is DoubleClickPacket doubleClickPacket)
        {
            return await HandleDoubleClickAsync(session, doubleClickPacket);
        }

        return true;
    }

    private async Task DispatchItemWearChange(Serial characterId)
    {
        var mobile = await _mobileService.GetAsync(characterId);

        if (mobile is null)
        {
            return;
        }

        var equippedItems = new List<UOItemEntity>();

        foreach (var itemId in mobile.EquippedItemIds.Values)
        {
            if (itemId == Serial.Zero)
            {
                continue;
            }

            var equippedItem = await _itemService.GetItemAsync(itemId);

            if (equippedItem is null)
            {
                continue;
            }

            equippedItems.Add(equippedItem);
        }

        mobile.HydrateEquipmentRuntime(equippedItems);

        var sector = _spatialWorldService.GetSectorByLocation(mobile.MapId, mobile.Location);

        var sessionIdsToNotify = new HashSet<long>();

        if (_gameNetworkSessionService.TryGetByCharacterId(characterId, out var sourceSession))
        {
            sessionIdsToNotify.Add(sourceSession.SessionId);
        }

        if (sector is null)
        {
            foreach (var sessionId in sessionIdsToNotify)
            {
                EnqueueVisibleWornItemsForSession(sessionId, mobile);
            }

            return;
        }

        var updateRadius = _spatialWorldService.GetUpdateBroadcastSectorRadius();

        for (var sectorX = sector.SectorX - updateRadius;
             sectorX <= sector.SectorX + updateRadius;
             sectorX++)
        {
            for (var sectorY = sector.SectorY - updateRadius;
                 sectorY <= sector.SectorY + updateRadius;
                 sectorY++)
            {
                var players = _spatialWorldService.GetPlayersInSector(mobile.MapId, sectorX, sectorY);

                foreach (var player in players)
                {
                    if (_gameNetworkSessionService.TryGetByCharacterId(player.Id, out var session))
                    {
                        sessionIdsToNotify.Add(session.SessionId);
                    }
                }
            }
        }

        foreach (var sessionId in sessionIdsToNotify)
        {
            EnqueueVisibleWornItemsForSession(sessionId, mobile);
        }
    }

    private async Task DropItemInContainerAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        var item = await _itemService.GetItemAsync(dropItemPacket.ItemSerial);

        if (item is null)
        {
            return;
        }

        var destinationContainer = await _itemService.GetItemAsync(dropItemPacket.DestinationSerial);

        if (destinationContainer is null)
        {
            return;
        }

        var containerToRefreshId = destinationContainer.Id;

        if (!destinationContainer.IsContainer &&
            destinationContainer.IsStackable &&
            destinationContainer.ItemId == item.ItemId)
        {
            // Check if destination container is stackable with the dropped item and stack if possible.
            destinationContainer.Amount += item.Amount;
            await _itemService.UpsertItemAsync(destinationContainer);
            await _itemService.DeleteItemAsync(item.Id);

            if (destinationContainer.ParentContainerId != Serial.Zero)
            {
                containerToRefreshId = destinationContainer.ParentContainerId;
            }
        }
        else
        {
            await _itemService.MoveItemToContainerAsync(
                item.Id,
                destinationContainer.Id,
                new(dropItemPacket.Location.X, dropItemPacket.Location.Y),
                session.SessionId
            );
        }

        destinationContainer = await _itemService.GetItemAsync(containerToRefreshId);

        if (destinationContainer is null)
        {
            return;
        }

        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(destinationContainer));
    }

    private async Task DropItemOnGroundAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        var mapId = session.Character?.MapId ?? 0;
        var dropResult = await _itemService.DropItemToGroundAsync(
                             dropItemPacket.ItemSerial,
                             dropItemPacket.Location,
                             mapId,
                             session.SessionId
                         );

        if (dropResult is null)
        {
            return;
        }

        await _gameEventBusService.PublishAsync(
            new DropItemToGroundEvent(
                session.SessionId,
                session.CharacterId,
                dropResult.Value.ItemId,
                dropResult.Value.SourceContainerId,
                dropResult.Value.OldLocation,
                dropResult.Value.NewLocation
            )
        );

        if (dropResult.Value.SourceContainerId == Serial.Zero)
        {
            return;
        }

        var sourceContainer = await _itemService.GetItemAsync(dropResult.Value.SourceContainerId);
        Enqueue(session, new DrawContainerAndAddItemCombinedPacket(sourceContainer));
    }

    private void EnqueueVisibleWornItemsForSession(long sessionId, UOMobileEntity mobile)
        => WornItemPacketHelper.EnqueueVisibleWornItems(mobile, packet => Enqueue(sessionId, packet));

    private async Task<bool> HandleDoubleClickAsync(GameSession session, DoubleClickPacket doubleClickPacket)
    {
        if (doubleClickPacket.TargetSerial.IsMobile)
        {
            await _gameEventBusService.PublishAsync(
                new MobileDoubleClickEvent(
                    session.SessionId,
                    doubleClickPacket.TargetSerial
                )
            );

            if (_characterService is null)
            {
                return true;
            }

            var mobile = await _characterService.GetCharacterAsync(doubleClickPacket.TargetSerial);

            if (mobile is null)
            {
                return true;
            }

            if (mobile.Body is { IsAnimal: false, IsMonster: false })
            {
                Enqueue(session, new PaperdollPacket(mobile));
            }

            return true;
        }

        var (canInteract, resolvedItem) = await ValidateGroundItemInteractionAsync(session, doubleClickPacket.TargetSerial);

        if (!canInteract)
        {
            return true;
        }

        var item = resolvedItem ?? await _itemService.GetItemAsync(doubleClickPacket.TargetSerial);

        if (item is null)
        {
            return true;
        }

        if (_itemScriptDispatcher?.HasHook(item, "double_click") != false)
        {
            await _gameEventBusService.PublishAsync(
                new ItemDoubleClickEvent(
                    session.SessionId,
                    doubleClickPacket.TargetSerial
                )
            );
        }

        if (TryEnqueueBook(session, item))
        {
            return true;
        }

        if (item.IsContainer)
        {
            Enqueue(session, new DrawContainerAndAddItemCombinedPacket(item));
        }

        return true;
    }

    private async Task<bool> HandleDropItemAsync(GameSession session, DropItemPacket dropItemPacket)
    {
        if (!_playerDragService.TryGet(session.SessionId, out var dragState) ||
            dragState.ItemId != dropItemPacket.ItemSerial)
        {
            _logger.Warning(
                "Drop rejected Session={SessionId} ItemId={ItemId}: no matching pending drag state",
                session.SessionId,
                dropItemPacket.ItemSerial
            );

            return false;
        }

        if (!dropItemPacket.IsGroundDrop)
        {
            await DropItemInContainerAsync(session, dropItemPacket);
            _playerDragService.Clear(session.SessionId);

            return true;
        }

        await DropItemOnGroundAsync(session, dropItemPacket);
        _playerDragService.Clear(session.SessionId);

        return true;
    }

    private async Task<bool> HandleDropWearItemAsync(GameSession session, DropWearItemPacket dropWearItemPacket)
    {
        if (session.Character is null || session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (dropWearItemPacket.PlayerSerial != session.CharacterId)
        {
            _logger.Warning(
                "DropWear rejected Session={SessionId} ItemId={ItemId}: target player mismatch packet={PacketPlayerId} session={SessionPlayerId}",
                session.SessionId,
                dropWearItemPacket.ItemSerial,
                dropWearItemPacket.PlayerSerial,
                session.CharacterId
            );

            return false;
        }

        if (!IsValidWearLayer(dropWearItemPacket.Layer))
        {
            _logger.Warning(
                "DropWear rejected Session={SessionId} ItemId={ItemId}: invalid requested layer {Layer}",
                session.SessionId,
                dropWearItemPacket.ItemSerial,
                dropWearItemPacket.Layer
            );

            return false;
        }

        await _itemService.EquipItemAsync(
            dropWearItemPacket.ItemSerial,
            session.CharacterId,
            dropWearItemPacket.Layer
        );

        await DispatchItemWearChange(session.CharacterId);

        return true;
    }

    private async Task<bool> HandlePickUpItemAsync(GameSession session, PickUpItemPacket pickUpItemPacket)
    {
        var item = await _itemService.GetItemAsync(pickUpItemPacket.ItemSerial);

        if (item is null)
        {
            return false;
        }

        var requestedAmount = Math.Max(1, pickUpItemPacket.StackAmount);
        var pickedAmount = Math.Min(requestedAmount, Math.Max(1, item.Amount));

        if (item.Amount > pickedAmount)
        {
            var sourceContainerId = item.ParentContainerId;
            var sourceLocation = item.Location;
            var sourceContainerPosition = item.ContainerPosition;
            var container = await _itemService.GetItemAsync(item.ParentContainerId);

            if (container is null)
            {
                return false;
            }

            var clonedItem = await _itemService.CloneAsync(item.Id);

            if (clonedItem is null)
            {
                return false;
            }

            // Keep original serial as the dragged stack so DropItem packet item serial still matches pending drag state.
            item.Amount = pickedAmount;
            item.ParentContainerId = Serial.Zero;
            item.ContainerPosition = Point2D.Zero;
            item.Location = sourceLocation;

            // Persist the remainder in the original container with a new serial.
            clonedItem.Amount = Math.Max(1, clonedItem.Amount - pickedAmount);
            clonedItem.ParentContainerId = sourceContainerId;
            clonedItem.ContainerPosition = sourceContainerPosition;
            clonedItem.Location = sourceLocation;

            await _itemService.UpsertItemsAsync(clonedItem, item);

            _playerDragService.SetPending(
                session.SessionId,
                item.Id,
                item.Amount,
                sourceContainerId,
                sourceLocation
            );

            if (sourceContainerId != Serial.Zero)
            {
                var sourceContainer = await _itemService.GetItemAsync(sourceContainerId);

                if (sourceContainer is not null)
                {
                    Enqueue(session, new DrawContainerAndAddItemCombinedPacket(sourceContainer));
                }
            }

            return true;
        }

        _playerDragService.SetPending(
            session.SessionId,
            item.Id,
            pickedAmount,
            item.ParentContainerId,
            item.Location
        );

        return true;
    }

    private async Task<bool> HandleSingleClickAsync(GameSession session, SingleClickPacket singleClickPacket)
    {
        var (canInteract, resolvedItem) = await ValidateGroundItemInteractionAsync(session, singleClickPacket.TargetSerial);

        if (!canInteract)
        {
            return true;
        }

        if (resolvedItem is null)
        {
            return true;
        }

        if (_itemScriptDispatcher?.HasHook(resolvedItem, "single_click") != false)
        {
            await _gameEventBusService.PublishAsync(
                new ItemSingleClickEvent(
                    session.SessionId,
                    singleClickPacket.TargetSerial
                )
            );
        }

        return true;
    }

    private async Task<bool> HandleBookPagesAsync(GameSession session, BookPagesPacket bookPagesPacket)
    {
        var item = await _itemService.GetItemAsync((Serial)bookPagesPacket.BookSerial);

        if (item is null || !TryReadBook(item, out _, out _, out var content))
        {
            return true;
        }

        if (bookPagesPacket.Pages.Any(static page => !page.IsPageRequest))
        {
            if (!IsWritableBook(item) || !await CanWriteBookAsync(session, item))
            {
                return true;
            }

            ApplyBookPageUpdates(item, bookPagesPacket.Pages);
            await _itemService.UpsertItemAsync(item);

            return true;
        }

        var requestedPages = BuildRequestedBookPages(content, bookPagesPacket.Pages);

        if (requestedPages.Count == 0)
        {
            return true;
        }

        var response = new BookPagesPacket
        {
            BookSerial = bookPagesPacket.BookSerial,
            PageCount = (ushort)requestedPages.Count
        };

        response.Pages.AddRange(requestedPages);
        Enqueue(session, response);

        return true;
    }

    private async Task<bool> HandleBookHeaderAsync(GameSession session, BookHeaderOldPacket bookHeaderOldPacket)
    {
        var item = await _itemService.GetItemAsync((Serial)bookHeaderOldPacket.BookSerial);

        if (item is null || !IsWritableBook(item) || !await CanWriteBookAsync(session, item))
        {
            return true;
        }

        item.SetCustomString(BookTemplateParamKeys.Title, SanitizeBookText(bookHeaderOldPacket.Title));
        item.SetCustomString(BookTemplateParamKeys.Author, SanitizeBookText(bookHeaderOldPacket.Author));
        await _itemService.UpsertItemAsync(item);

        return true;
    }

    private static bool IsGroundItem(UOItemEntity item)
        => item.ParentContainerId == Serial.Zero && item.EquippedMobileId == Serial.Zero;

    private bool TryEnqueueBook(GameSession session, UOItemEntity item)
    {
        if (!TryReadBook(item, out var title, out var author, out var content))
        {
            return false;
        }

        var pages = BuildBookPages(content);
        var header = new BookHeaderNewPacket
        {
            BookSerial = item.Id.Value,
            Flag1 = false,
            IsWritable = IsWritableBook(item),
            PageCount = (ushort)pages.Count,
            Title = title,
            Author = author
        };

        var packet = new BookPagesPacket
        {
            BookSerial = item.Id.Value,
            PageCount = (ushort)pages.Count
        };

        for (var i = 0; i < pages.Count; i++)
        {
            var page = new BookPageEntry
            {
                PageNumber = (ushort)(i + 1),
                LineCount = (ushort)pages[i].Count
            };

            page.Lines.AddRange(pages[i]);
            packet.Pages.Add(page);
        }

        Enqueue(session, header);
        Enqueue(session, packet);

        return true;
    }

    private static bool TryReadBook(UOItemEntity item, out string title, out string author, out string content)
    {
        title = string.Empty;
        author = string.Empty;
        content = string.Empty;

        if (!item.TryGetCustomString(BookTemplateParamKeys.Title, out title) ||
            !item.TryGetCustomString(BookTemplateParamKeys.Author, out author) ||
            !item.TryGetCustomString(BookTemplateParamKeys.Content, out content))
        {
            return false;
        }

        return true;
    }

    private static bool IsWritableBook(UOItemEntity item)
    {
        if (item.TryGetCustomBoolean(BookTemplateParamKeys.Writable, out var writable))
        {
            return writable;
        }

        return item.TryGetCustomString(BookTemplateParamKeys.Writable, out var stringValue) &&
               bool.TryParse(stringValue, out writable) &&
               writable;
    }

    private async Task<bool> CanWriteBookAsync(GameSession session, UOItemEntity item)
    {
        if (session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (item.EquippedMobileId == session.CharacterId)
        {
            return true;
        }

        var mobile = await _mobileService.GetAsync(session.CharacterId);

        if (mobile is null)
        {
            return false;
        }

        var backpackId = ResolveBackpackId(mobile);

        if (backpackId == Serial.Zero)
        {
            return false;
        }

        var currentContainerId = item.ParentContainerId;

        while (currentContainerId != Serial.Zero)
        {
            if (currentContainerId == backpackId)
            {
                return true;
            }

            var container = await _itemService.GetItemAsync(currentContainerId);

            if (container is null)
            {
                return false;
            }

            if (container.EquippedMobileId == session.CharacterId)
            {
                return true;
            }

            currentContainerId = container.ParentContainerId;
        }

        return false;
    }

    private static Serial ResolveBackpackId(UOMobileEntity mobile)
    {
        if (mobile.BackpackId != Serial.Zero)
        {
            return mobile.BackpackId;
        }

        return mobile.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId)
                   ? equippedBackpackId
                   : Serial.Zero;
    }

    private static List<BookPageEntry> BuildRequestedBookPages(string content, IReadOnlyList<BookPageEntry> requestedPages)
    {
        var allPages = BuildBookPages(content);
        var responsePages = new List<BookPageEntry>();

        foreach (var requestedPage in requestedPages)
        {
            if (!requestedPage.IsPageRequest)
            {
                continue;
            }

            var pageIndex = requestedPage.PageNumber - 1;

            if (pageIndex < 0 || pageIndex >= allPages.Count)
            {
                continue;
            }

            var responsePage = new BookPageEntry
            {
                PageNumber = requestedPage.PageNumber,
                LineCount = (ushort)allPages[pageIndex].Count
            };

            responsePage.Lines.AddRange(allPages[pageIndex]);
            responsePages.Add(responsePage);
        }

        return responsePages;
    }

    private static List<List<string>> BuildBookPages(string content)
    {
        var normalized = string.IsNullOrWhiteSpace(content)
                             ? string.Empty
                             : content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Length == 0 ? [] : normalized.Split('\n').ToList();

        if (lines.Count == 0)
        {
            return [[]];
        }

        var pages = new List<List<string>>();

        for (var index = 0; index < lines.Count; index += BookLinesPerPage)
        {
            pages.Add(lines.Skip(index).Take(BookLinesPerPage).ToList());
        }

        return pages;
    }

    private static void ApplyBookPageUpdates(UOItemEntity item, IReadOnlyList<BookPageEntry> updatedPages)
    {
        item.TryGetCustomString(BookTemplateParamKeys.Content, out var content);
        var lines = SplitBookContent(content).ToList();

        foreach (var page in updatedPages.Where(static page => !page.IsPageRequest))
        {
            var startLineIndex = Math.Max(0, page.PageNumber - 1) * BookLinesPerPage;
            var lineCount = page.LineCount == 0 ? page.Lines.Count : Math.Min(page.LineCount, page.Lines.Count);

            while (lines.Count < startLineIndex)
            {
                lines.Add(string.Empty);
            }

            for (var i = 0; i < lineCount; i++)
            {
                var targetIndex = startLineIndex + i;

                while (lines.Count <= targetIndex)
                {
                    lines.Add(string.Empty);
                }

                lines[targetIndex] = page.Lines[i];
            }
        }

        item.SetCustomString(BookTemplateParamKeys.Content, string.Join('\n', lines));
    }

    private static IEnumerable<string> SplitBookContent(string? content)
    {
        var normalized = string.IsNullOrWhiteSpace(content)
                             ? string.Empty
                             : content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        return normalized.Length == 0 ? [] : normalized.Split('\n');
    }

    private static string SanitizeBookText(string value)
        => new string(value.Select(static c => char.IsControl(c) ? ' ' : c).ToArray()).TrimEnd();

    private static bool IsValidWearLayer(ItemLayerType layer)
    {
        if (layer < ItemLayerType.FirstValid || layer > ItemLayerType.LastUserValid)
        {
            return false;
        }

        return layer is not ItemLayerType.Backpack and not ItemLayerType.Bank;
    }

    private async Task<(bool CanInteract, UOItemEntity? Item)> ValidateGroundItemInteractionAsync(
        GameSession session,
        Serial itemId
    )
    {
        var item = await _itemService.GetItemAsync(itemId);

        if (item is null || !IsGroundItem(item) || session.AccountType >= AccountType.GameMaster)
        {
            return (true, item);
        }

        var character = session.Character;

        if (character is null ||
            character.MapId != item.MapId ||
            !character.Location.InRange(item.Location, GroundItemInteractionRange))
        {
            _logger.Debug(
                "Item interaction rejected Session={SessionId} ItemId={ItemId}: out of range for ground item",
                session.SessionId,
                itemId
            );

            return (false, item);
        }

        return (true, item);
    }
}
