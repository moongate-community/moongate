using Moongate.Network.Packets.Incoming.Trading;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.Trading;
using Moongate.Server.Data.Internal.Interaction;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.SellProfiles;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

public sealed class PlayerSellBuyService : IPlayerSellBuyService
{
    private const int DefaultShopContainerItemId = 0x0E75;
    private const string SellProfileIdKey = "sell_profile_id";

    private readonly ICharacterService _characterService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly ISellProfileTemplateService _sellProfileTemplateService;
    private readonly IGameNetworkSessionService _sessionService;
    private readonly Dictionary<(Serial VendorId, string TemplateId), int> _vendorStock = [];
    private readonly Dictionary<long, PendingVendorBuyState> _pendingBuyStates = [];
    private readonly Dictionary<long, PendingVendorSellState> _pendingSellStates = [];

    public PlayerSellBuyService(
        IGameNetworkSessionService sessionService,
        IMobileService mobileService,
        ICharacterService characterService,
        IItemFactoryService itemFactoryService,
        IItemService itemService,
        ISellProfileTemplateService sellProfileTemplateService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _sessionService = sessionService;
        _mobileService = mobileService;
        _characterService = characterService;
        _itemFactoryService = itemFactoryService;
        _itemService = itemService;
        _sellProfileTemplateService = sellProfileTemplateService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public Task HandleBuyItemsAsync(long sessionId, BuyItemsPacket packet, CancellationToken cancellationToken = default)
        => HandleBuyItemsCoreAsync(sessionId, packet, cancellationToken);

    public Task HandleSellListReplyAsync(
        long sessionId,
        SellListReplyPacket packet,
        CancellationToken cancellationToken = default
    )
        => HandleSellListReplyCoreAsync(sessionId, packet, cancellationToken);

    public async Task HandleVendorBuyRequestAsync(
        long sessionId,
        Serial vendorSerial,
        CancellationToken cancellationToken = default
    )
    {
        var context = await TryResolveContextAsync(sessionId, vendorSerial, cancellationToken);

        if (context is null)
        {
            return;
        }

        var session = context.Value.Session;
        var character = context.Value.Character;
        var vendor = context.Value.Vendor;
        var sellProfile = context.Value.SellProfile;

        var buyPackSerial = NextVirtualItemSerial();
        var resalePackSerial = NextVirtualItemSerial();
        var buyState = new PendingVendorBuyState(vendorSerial, buyPackSerial, resalePackSerial);

        var buyContainer = new UOItemEntity
        {
            Id = buyPackSerial,
            ItemId = DefaultShopContainerItemId,
            GumpId = 0x0030
        };

        var listPacket = new VendorBuyListPacket
        {
            ShopContainerSerial = buyPackSerial
        };

        foreach (var vendorItem in sellProfile.VendorItems.Where(static item => item.Enabled))
        {
            if (!_itemFactoryService.TryGetItemTemplate(vendorItem.ItemTemplateId, out var template) || template is null)
            {
                continue;
            }

            var stock = ResolveVendorStock(vendor.Id, vendorItem);

            if (stock <= 0)
            {
                continue;
            }

            var displaySerial = NextVirtualItemSerial();
            var itemId = ParseItemId(template);
            var amount = Math.Max(1, stock);
            var description = string.IsNullOrWhiteSpace(template.Name) ? vendorItem.ItemTemplateId : template.Name;

            buyState.Entries[displaySerial] = new(vendorItem.ItemTemplateId, vendorItem.Price, stock);
            buyContainer.AddItem(
                new UOItemEntity
                {
                    ItemId = itemId,
                    Id = displaySerial,
                    Amount = amount,
                    Hue = template.Hue.Resolve()
                },
                new(buyContainer.Items.Count + 1, 1)
            );
            listPacket.Entries.Add(
                new()
                {
                    Price = vendorItem.Price,
                    Description = description
                }
            );
        }

        if (buyContainer.Items.Count == 0)
        {
            return;
        }

        await EnsureGoldContainersLoadedAsync(character);
        _pendingBuyStates[sessionId] = buyState;
        _outgoingPacketQueue.Enqueue(sessionId, CreateVendorPackPacket(vendor, buyPackSerial, ItemLayerType.ShopBuy));
        _outgoingPacketQueue.Enqueue(sessionId, CreateVendorPackPacket(vendor, resalePackSerial, ItemLayerType.ShopResale));
        _outgoingPacketQueue.Enqueue(sessionId, new AddMultipleItemsToContainerPacket(buyContainer));
        _outgoingPacketQueue.Enqueue(sessionId, listPacket);
        _outgoingPacketQueue.Enqueue(
            sessionId,
            new DrawContainerPacket(
                new()
                {
                    Id = vendor.Id,
                    ItemId = DefaultShopContainerItemId,
                    GumpId = 0x0030
                }
            )
        );
        _outgoingPacketQueue.Enqueue(sessionId, new PlayerStatusPacket(character, 1));
    }

    public async Task HandleVendorSellRequestAsync(
        long sessionId,
        Serial vendorSerial,
        CancellationToken cancellationToken = default
    )
    {
        var context = await TryResolveContextAsync(sessionId, vendorSerial, cancellationToken);

        if (context is null)
        {
            return;
        }

        var session = context.Value.Session;
        var character = context.Value.Character;
        var vendor = context.Value.Vendor;
        var sellProfile = context.Value.SellProfile;

        var backpack = await _characterService.GetBackpackWithItemsAsync(character);

        if (backpack is null)
        {
            return;
        }

        var sellPacket = new VendorSellListPacket
        {
            VendorSerial = vendor.Id
        };
        var sellState = new PendingVendorSellState(vendor.Id);

        foreach (var item in EnumerateItemsRecursive(backpack))
        {
            var match = sellProfile.AcceptedItems
                                   .Where(static accepted => accepted.Enabled)
                                   .FirstOrDefault(accepted => MatchesAcceptedItem(item, accepted));

            if (match is null)
            {
                continue;
            }

            sellState.Entries[item.Id] = new(item.Id, match.Price, item.Amount, match.ItemTemplateId);
            sellPacket.Entries.Add(
                new()
                {
                    ItemSerial = item.Id,
                    ItemId = item.ItemId,
                    Hue = item.Hue,
                    Amount = item.Amount,
                    Price = match.Price,
                    Name = item.Name ?? match.ItemTemplateId
                }
            );
        }

        if (sellPacket.Entries.Count == 0)
        {
            return;
        }

        await EnsureGoldContainersLoadedAsync(character);
        _pendingSellStates[sessionId] = sellState;
        _outgoingPacketQueue.Enqueue(sessionId, sellPacket);
    }

    private static void ConsumeGoldRecursive(
        UOItemEntity container,
        ref int remaining,
        ICollection<UOItemEntity> changedGoldStacks,
        ICollection<UOItemEntity> deletedGoldStacks
    )
    {
        for (var i = container.Items.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var child = container.Items[i];

            if (child.ItemId == 0x0EED)
            {
                var consumed = Math.Min(remaining, child.Amount);
                child.Amount -= consumed;
                remaining -= consumed;

                if (child.Amount <= 0)
                {
                    container.RemoveItem(child.Id);
                    deletedGoldStacks.Add(child);
                }
                else
                {
                    changedGoldStacks.Add(child);
                }

                continue;
            }

            ConsumeGoldRecursive(child, ref remaining, changedGoldStacks, deletedGoldStacks);
        }
    }

    private WornItemPacket CreateVendorPackPacket(UOMobileEntity vendor, Serial itemSerial, ItemLayerType layer)
        => new(vendor, new(itemSerial, DefaultShopContainerItemId, 0), layer);

    private async Task<bool> CreditGoldAsync(UOMobileEntity character, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        var backpack = await _characterService.GetBackpackWithItemsAsync(character);

        if (backpack is null)
        {
            return false;
        }

        var goldStack = FindGoldStack(backpack);

        if (goldStack is not null)
        {
            goldStack.Amount += amount;
            await _itemService.UpsertItemAsync(goldStack);

            return true;
        }

        if (!_itemFactoryService.TryGetItemTemplate("gold", out _))
        {
            return false;
        }

        var goldItem = _itemFactoryService.CreateItemFromTemplate("gold");
        goldItem.Amount = amount;
        backpack.AddItem(goldItem, new(1, 1));
        await _itemService.CreateItemAsync(goldItem);

        return true;
    }

    private async Task EnsureGoldContainersLoadedAsync(UOMobileEntity character)
    {
        var backpack = await _characterService.GetBackpackWithItemsAsync(character);

        if (backpack is not null)
        {
            character.AddEquippedItem(ItemLayerType.Backpack, backpack);
        }

        var bankBox = await _characterService.GetBankBoxWithItemsAsync(character);

        if (bankBox is not null)
        {
            character.AddEquippedItem(ItemLayerType.Bank, bankBox);
        }
    }

    private static IEnumerable<UOItemEntity> EnumerateItemsRecursive(UOItemEntity container)
    {
        foreach (var item in container.Items)
        {
            yield return item;

            foreach (var child in EnumerateItemsRecursive(item))
            {
                yield return child;
            }
        }
    }

    private static UOItemEntity? FindGoldStack(UOItemEntity container)
        => EnumerateItemsRecursive(container).FirstOrDefault(static item => item.ItemId == 0x0EED);

    private async Task HandleBuyItemsCoreAsync(long sessionId, BuyItemsPacket packet, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (!_pendingBuyStates.TryGetValue(sessionId, out var state) ||
            state.VendorSerial != packet.VendorSerial ||
            !_sessionService.TryGet(sessionId, out var session) ||
            session.Character is null)
        {
            return;
        }

        var character = session.Character;
        var backpack = await _characterService.GetBackpackWithItemsAsync(character);
        var bankBox = await _characterService.GetBankBoxWithItemsAsync(character);

        if (backpack is null)
        {
            return;
        }

        var totalCost = 0;
        var purchases = new List<(PendingVendorBuyEntry Entry, int Amount)>();

        foreach (var item in packet.Items)
        {
            if (!state.Entries.TryGetValue(item.ItemSerial, out var entry))
            {
                return;
            }

            if (item.Amount <= 0 || item.Amount > entry.Stock)
            {
                return;
            }

            purchases.Add((entry, item.Amount));
            totalCost += entry.Price * item.Amount;
        }

        if (!HasEnoughGold(backpack, bankBox, totalCost))
        {
            return;
        }

        if (!TryConsumeGold(backpack, bankBox, totalCost, out var changedGoldStacks, out var deletedGoldStacks))
        {
            return;
        }

        foreach (var changedGoldStack in changedGoldStacks)
        {
            await _itemService.UpsertItemAsync(changedGoldStack);
        }

        foreach (var deletedGoldStack in deletedGoldStacks)
        {
            _ = await _itemService.DeleteItemAsync(deletedGoldStack.Id);
        }

        foreach (var (entry, amount) in purchases)
        {
            var boughtItem = _itemFactoryService.CreateItemFromTemplate(entry.ItemTemplateId);
            boughtItem.Amount = amount;
            backpack.AddItem(boughtItem, new(backpack.Items.Count + 1, 1));
            await _itemService.CreateItemAsync(boughtItem);

            var stockKey = (state.VendorSerial, entry.ItemTemplateId);

            if (_vendorStock.TryGetValue(stockKey, out var stock))
            {
                _vendorStock[stockKey] = Math.Max(0, stock - amount);
            }
        }

        await EnsureGoldContainersLoadedAsync(character);
        _outgoingPacketQueue.Enqueue(sessionId, new DrawContainerAndAddItemCombinedPacket(backpack));
        _outgoingPacketQueue.Enqueue(sessionId, new PlayerStatusPacket(character, 1));
        _pendingBuyStates.Remove(sessionId);
    }

    private async Task HandleSellListReplyCoreAsync(
        long sessionId,
        SellListReplyPacket packet,
        CancellationToken cancellationToken
    )
    {
        _ = cancellationToken;

        if (!_pendingSellStates.TryGetValue(sessionId, out var state) ||
            state.VendorSerial != packet.VendorSerial ||
            !_sessionService.TryGet(sessionId, out var session) ||
            session.Character is null)
        {
            return;
        }

        var character = session.Character;
        var backpack = await _characterService.GetBackpackWithItemsAsync(character);

        if (backpack is null)
        {
            return;
        }

        var totalGold = 0;

        foreach (var sold in packet.Items)
        {
            if (!state.Entries.TryGetValue(sold.ItemSerial, out var entry) ||
                sold.Amount <= 0 ||
                sold.Amount > entry.Stock ||
                !TryFindContainedItem(backpack, sold.ItemSerial, out var parent, out var item) ||
                item is null ||
                parent is null ||
                sold.Amount > item.Amount)
            {
                return;
            }

            totalGold += entry.Price * sold.Amount;

            if (sold.Amount == item.Amount)
            {
                parent.RemoveItem(item.Id);
                _ = await _itemService.DeleteItemAsync(item.Id);
            }
            else
            {
                item.Amount -= sold.Amount;
                await _itemService.UpsertItemAsync(item);
            }
        }

        _ = await CreditGoldAsync(character, totalGold);
        await EnsureGoldContainersLoadedAsync(character);
        _outgoingPacketQueue.Enqueue(sessionId, new DrawContainerAndAddItemCombinedPacket(backpack));
        _outgoingPacketQueue.Enqueue(sessionId, new PlayerStatusPacket(character, 1));
        _pendingSellStates.Remove(sessionId);
    }

    private bool MatchesAcceptedItem(UOItemEntity item, SellProfileAcceptedItemDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ItemTemplateId))
        {
            return false;
        }

        if (item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var runtimeTemplateId) &&
            !string.IsNullOrWhiteSpace(runtimeTemplateId))
        {
            return string.Equals(
                runtimeTemplateId.Trim(),
                definition.ItemTemplateId.Trim(),
                StringComparison.OrdinalIgnoreCase
            );
        }

        if (!_itemFactoryService.TryGetItemTemplate(definition.ItemTemplateId, out var template) || template is null)
        {
            return false;
        }

        return item.ItemId == ParseItemId(template);
    }

    private static Serial NextVirtualItemSerial()
        => (Serial)(Serial.ItemOffset + (uint)Random.Shared.Next(1, int.MaxValue / 2));

    private static int ParseItemId(ItemTemplateDefinition template)
        => template.ItemId.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
               ? Convert.ToInt32(template.ItemId[2..], 16)
               : int.Parse(template.ItemId);

    private int ResolveVendorStock(Serial vendorId, SellProfileVendorItemDefinition vendorItem)
    {
        var key = (vendorId, vendorItem.ItemTemplateId);

        if (_vendorStock.TryGetValue(key, out var stock))
        {
            return stock;
        }

        stock = Math.Max(0, vendorItem.MaxStock);
        _vendorStock[key] = stock;

        return stock;
    }

    private static bool TryConsumeGold(
        UOItemEntity? backpack,
        UOItemEntity? bankBox,
        int amount,
        out List<UOItemEntity> changedGoldStacks,
        out List<UOItemEntity> deletedGoldStacks
    )
    {
        changedGoldStacks = [];
        deletedGoldStacks = [];

        if (amount <= 0)
        {
            return true;
        }

        var remaining = amount;

        if (backpack is not null)
        {
            ConsumeGoldRecursive(backpack, ref remaining, changedGoldStacks, deletedGoldStacks);
        }

        if (remaining > 0 && bankBox is not null)
        {
            ConsumeGoldRecursive(bankBox, ref remaining, changedGoldStacks, deletedGoldStacks);
        }

        return remaining == 0;
    }

    private static bool HasEnoughGold(UOItemEntity? backpack, UOItemEntity? bankBox, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return CountGold(backpack) + CountGold(bankBox) >= amount;
    }

    private static int CountGold(UOItemEntity? container)
    {
        if (container is null)
        {
            return 0;
        }

        var total = 0;

        foreach (var item in EnumerateItemsRecursive(container))
        {
            if (item.ItemId == 0x0EED)
            {
                total += item.Amount;
            }
        }

        return total;
    }

    private static bool TryFindContainedItem(
        UOItemEntity container,
        Serial itemSerial,
        out UOItemEntity? parent,
        out UOItemEntity? item
    )
    {
        foreach (var child in container.Items)
        {
            if (child.Id == itemSerial)
            {
                parent = container;
                item = child;

                return true;
            }

            if (TryFindContainedItem(child, itemSerial, out parent, out item))
            {
                return true;
            }
        }

        parent = null;
        item = null;

        return false;
    }

    private async Task<(GameSession Session, UOMobileEntity Character, UOMobileEntity Vendor, SellProfileTemplateDefinition
            SellProfile)?>
        TryResolveContextAsync(long sessionId, Serial vendorSerial, CancellationToken cancellationToken)
    {
        if (!_sessionService.TryGet(sessionId, out var session) || session.Character is null)
        {
            return null;
        }

        var character = session.Character;
        var vendor = await _mobileService.GetAsync(vendorSerial, cancellationToken);

        if (vendor is null)
        {
            return null;
        }

        if (vendor.MapId != character.MapId || !character.Location.InRange(vendor.Location, 4))
        {
            return null;
        }

        if (!vendor.TryGetCustomString(SellProfileIdKey, out var sellProfileId) ||
            string.IsNullOrWhiteSpace(sellProfileId) ||
            !_sellProfileTemplateService.TryGet(sellProfileId, out var sellProfile) ||
            sellProfile is null)
        {
            return null;
        }

        return (session, character, vendor, sellProfile);
    }
}
