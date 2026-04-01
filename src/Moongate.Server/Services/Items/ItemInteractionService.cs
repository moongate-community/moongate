using Moongate.Network.Packets.Incoming.Interaction;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Items;

public class ItemInteractionService : IItemInteractionService
{
    private const int GroundItemInteractionRange = 2;
    private const string RegularSpellbookTemplateId = "spellbook";

    private readonly ILogger _logger = Log.ForContext<ItemInteractionService>();
    private readonly IItemService _itemService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IItemScriptDispatcher? _itemScriptDispatcher;
    private readonly ICharacterService? _characterService;
    private readonly IItemBookService _itemBookService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly ILootGenerationService? _lootGenerationService;

    public ItemInteractionService(
        IItemService itemService,
        IGameEventBusService gameEventBusService,
        IItemBookService itemBookService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IItemScriptDispatcher? itemScriptDispatcher = null,
        ICharacterService? characterService = null,
        ILootGenerationService? lootGenerationService = null
    )
    {
        _itemService = itemService;
        _gameEventBusService = gameEventBusService;
        _itemBookService = itemBookService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _itemScriptDispatcher = itemScriptDispatcher;
        _characterService = characterService;
        _lootGenerationService = lootGenerationService;
    }

    public async Task<bool> HandleDoubleClickAsync(
        GameSession session,
        DoubleClickPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        if (packet.TargetSerial.IsMobile)
        {
            await _gameEventBusService.PublishAsync(
                new MobileDoubleClickEvent(
                    session.SessionId,
                    packet.TargetSerial
                )
            );

            if (_characterService is null)
            {
                return true;
            }

            var mobile = await _characterService.GetCharacterAsync(packet.TargetSerial);

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

        var (canInteract, resolvedItem) = await ValidateGroundItemInteractionAsync(session, packet.TargetSerial);

        if (!canInteract)
        {
            return true;
        }

        var item = resolvedItem ?? await _itemService.GetItemAsync(packet.TargetSerial);

        if (item is null)
        {
            return true;
        }

        if (_itemScriptDispatcher?.HasHook(item, "double_click") != false)
        {
            await _gameEventBusService.PublishAsync(
                new ItemDoubleClickEvent(
                    session.SessionId,
                    packet.TargetSerial
                )
            );
        }

        if (TryEnqueueSpellbook(session, item))
        {
            return true;
        }

        if (await _itemBookService.TryEnqueueBookAsync(session, item))
        {
            return true;
        }

        if (item.IsContainer)
        {
            if (_lootGenerationService is not null)
            {
                item = await _lootGenerationService.EnsureLootGeneratedAsync(item, cancellationToken);
            }

            Enqueue(session, new DrawContainerAndAddItemCombinedPacket(item));

            if (CorpsePacketHelper.IsCorpse(item))
            {
                Enqueue(session, new CorpseClothingPacket(item));
            }
        }

        return true;
    }

    public async Task<bool> HandleSingleClickAsync(
        GameSession session,
        SingleClickPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var (canInteract, resolvedItem) = await ValidateGroundItemInteractionAsync(session, packet.TargetSerial);

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
                    packet.TargetSerial
                )
            );
        }

        return true;
    }

    private void Enqueue(GameSession session, IGameNetworkPacket packet)
        => _outgoingPacketQueue.Enqueue(session.SessionId, packet);

    private static ulong GetSpellbookContent(UOItemEntity item)
    {
        if (item.TryGetCustomInteger(ItemCustomParamKeys.Spellbook.Content, out var stored))
        {
            return unchecked((ulong)stored);
        }

        return 0UL;
    }

    private static bool IsRegularSpellbook(UOItemEntity item)
    {
        if (!item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) ||
            string.IsNullOrWhiteSpace(templateId))
        {
            return false;
        }

        return string.Equals(templateId, RegularSpellbookTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryEnqueueSpellbook(GameSession session, UOItemEntity item)
    {
        if (!IsRegularSpellbook(item))
        {
            return false;
        }

        Enqueue(session, new DisplaySpellbookAndContentsPacket(item, GetSpellbookContent(item)));

        return true;
    }

    private static bool IsGroundItem(UOItemEntity item)
        => item.ParentContainerId == Serial.Zero && item.EquippedMobileId == Serial.Zero;

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
