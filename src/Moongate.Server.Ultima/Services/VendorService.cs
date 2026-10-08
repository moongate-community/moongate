using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Internal.Vendors;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Shops;
using Moongate.Server.Ultima.Data.Vendors;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.Vendors;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Types.Templates;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     The shop window of the NPC vendors, as ModernUO's: the goods are counters kept per vendor, the lines of a window are
///     virtual serials counting down from <see cref="Serial.MaxVirtual" /> that the client sees in two virtual containers
///     the vendor wears, and a purchase is everything or nothing. Gold is taken from the backpack, then from the bank
///     when the total is 2000 or more. Game loop only.
/// </summary>
public sealed class VendorService : IVendorService
{
    private const int ViewRange = 10;
    private const int VendorGump = 0x30;
    private const int MaxReplyLines = 100;
    private const int MaxWindowLines = 250;
    private const int BankFrom = 2000;
    private const int GraphicMask = 0x3FFF;
    private const int GraphicCliloc = 1020000;
    private const int HighGraphicCliloc = 1078872;
    private const int HighGraphic = 0x4000;
    private const int ShopContainerGraphic = 0x0E75;
    private const int StockPruneAbove = 1024;
    private const int MaxSellList = 250;
    private const int MaxSellReplyLines = 100;
    private const int ClilocBackpackFull = 1048147;
    private const string NothingToSell = "You have nothing I would be interested in.";
    private const long VirtualHalf = Serial.MinVirtual + (Serial.MaxVirtual - Serial.MinVirtual) / 2;
    private const int ClilocMurdererRefused = 501522;
    private const int ClilocCannotAfford = 500192;
    private const int ClilocBankLacksFunds = 500191;
    private const int ClilocOrderCannotBeFulfilled = 500187;
    private const int ClilocPaidFromBackpack = 1151639;
    private const int ClilocPaidFromBank = 1151638;

    private readonly ILogger _logger = Log.ForContext<VendorService>();
    private readonly Dictionary<(Serial Vendor, string Line), int> _stock = new();
    private readonly IShopService _shops;
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IItemSerialPool _serials;
    private readonly IBankService _bank;
    private readonly IPacketSendService _sender;
    private readonly ISpeechService _speech;
    private readonly ILineOfSightService _sight;
    private readonly IRegionService _regions;
    private readonly IContainerCapacityService _capacity;
    private readonly IContainerLayoutService _layouts;
    private readonly IWeightService _weight;
    private readonly IWorldViewService _view;
    private readonly IFatigueService? _fatigue;
    private long _nextVirtual = Serial.MaxVirtual;

    public VendorService(
        IShopService shops,
        IItemTemplateService templates,
        ITileDataService tiles,
        IMobileService mobiles,
        IItemService items,
        IItemHandlingService handling,
        IItemSerialPool serials,
        IBankService bank,
        IPacketSendService sender,
        ISpeechService speech,
        ILineOfSightService sight,
        IRegionService regions,
        IContainerCapacityService capacity,
        IContainerLayoutService layouts,
        IWeightService weight,
        IWorldViewService view,
        IFatigueService? fatigue = null
    )
    {
        _shops = shops;
        _templates = templates;
        _tiles = tiles;
        _mobiles = mobiles;
        _items = items;
        _handling = handling;
        _serials = serials;
        _bank = bank;
        _sender = sender;
        _speech = speech;
        _sight = sight;
        _regions = regions;
        _capacity = capacity;
        _layouts = layouts;
        _weight = weight;
        _view = view;
        _fatigue = fatigue;
    }

    public bool OpenBuy(GameSession session, MobileEntity vendor)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(vendor);

        if (!_mobiles.TryGet(session.CharacterId, out var player) ||
            !vendor.IsNpc ||
            !_shops.TryGetFor(vendor, out var shop) ||
            !CanReach(player, vendor))
        {
            return false;
        }

        if (RefusesMurderer(session, player, vendor))
        {
            return false;
        }

        PruneStock();

        var shown = new List<VendorWindowLine>();

        foreach (var line in shop.Buy)
        {
            if (shown.Count < MaxWindowLines &&
                _templates.TryGet(line.Item, out var template) &&
                StockOf(vendor, line) > 0)
            {
                shown.Add(new(line, template, StockKey(line)));
            }
        }

        if (shown.Count == 0)
        {
            return false;
        }

        var shopContainer = NextVirtual();
        var resaleContainer = NextVirtual();
        var lines = new Dictionary<Serial, VendorWindowLine>(shown.Count);
        var entries = new List<ContainerItemEntry>(shown.Count);
        var names = new List<VendorBuyListEntry>(shown.Count);

        for (var index = 0; index < shown.Count; index++)
        {
            var line = shown[index];
            var serial = NextVirtual();
            lines[serial] = line;
            entries.Add(
                new(
                    serial,
                    (int)line.Template.ItemId.Value,
                    StockOf(vendor, line.Line),
                    index + 1,
                    1,
                    0,
                    shopContainer,
                    new((ushort)line.Line.Hue)
                )
            );
            names.Add(new(line.Line.Price, NameOf(line)));
        }

        // The client expects the items from the last to the first, and the names in the order of the lines.
        entries.Reverse();

        var id = session.SessionId;
        _sender.TrySend(id, new WornItemPacket(shopContainer, ShopContainerGraphic, LayerType.ShopBuy, vendor.Id, 0));
        _sender.TrySend(id, new WornItemPacket(resaleContainer, ShopContainerGraphic, LayerType.ShopResale, vendor.Id, 0));
        _sender.TrySend(id, ContainerContentPacket.Of(entries, session.UsesContainerGrid()));
        _sender.TrySend(id, new VendorBuyListPacket(shopContainer, names));
        _sender.TrySend(id, new DisplayContainerPacket(vendor.Id, VendorGump, session.UsesHighSeasContainers()));
        session.Set(VendorSessionKeys.Window, new VendorWindow(vendor.Id, shopContainer, lines));

        return true;
    }

    public void Buy(GameSession session, VendorBuyReplyPacket packet)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(packet);

        var window = session.Get(VendorSessionKeys.Window);

        if (window is null || packet.Lines.Count > MaxReplyLines)
        {
            return;
        }

        if (window.Vendor != packet.Vendor)
        {
            // The window of another vendor is open; the client waiting on this one is told it is over.
            _sender.TrySend(session.SessionId, new VendorEndPacket(packet.Vendor));

            return;
        }

        if (packet.Flag != VendorBuyReplyPacket.BuyFlag || packet.Lines.Count == 0)
        {
            End(session, window);

            return;
        }

        if (!_mobiles.TryGet(session.CharacterId, out var player) ||
            !_mobiles.TryGet(window.Vendor, out var vendor) ||
            !CanReach(player, vendor))
        {
            End(session, window);

            return;
        }

        if (RefusesMurderer(session, player, vendor))
        {
            End(session, window);

            return;
        }

        if (!TryWanted(vendor, window, packet, out var wanted))
        {
            End(session, window);

            return;
        }

        var total = wanted.Sum(pair => (long)pair.Key.Line.Price * pair.Value);

        if (total > int.MaxValue)
        {
            Refuse(session, window, player, ClilocCannotAfford);

            return;
        }

        var pays = IsStaff(session) ? 0 : (int)total;

        // Before any item is made: a serial taken for goods that are then refused is not given back.
        if (!CanAfford(player, pays, out var shortage))
        {
            Refuse(session, window, player, shortage);

            return;
        }

        var made = new List<ItemEntity>();
        var pieces = wanted.Sum(pair => IsStack(pair.Key.Template) ? 1 : pair.Value);

        if (pieces > _serials.Available || !TryMake(wanted, made))
        {
            Refuse(session, window, player, ClilocOrderCannotBeFulfilled);

            return;
        }

        if (!TryPay(player, pays, out var fromBank, out var refusal))
        {
            Refuse(session, window, player, refusal);

            return;
        }

        Deliver(player, made);

        foreach (var (line, amount) in wanted)
        {
            _stock[(vendor.Id, line.Stock)] = StockOf(vendor, line.Line) - amount;
        }

        End(session, window);

        if (pays > 0)
        {
            _speech.TellCliloc(player, fromBank ? ClilocPaidFromBank : ClilocPaidFromBackpack, pays.ToString());
        }

        _fatigue?.LoadChanged(session, player, true);
    }

    public bool OpenSell(GameSession session, MobileEntity vendor)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(vendor);

        if (!_mobiles.TryGet(session.CharacterId, out var player) ||
            !vendor.IsNpc ||
            !_shops.TryGetFor(vendor, out var shop) ||
            shop.Sell.Count == 0 ||
            !CanReach(player, vendor) ||
            RefusesMurderer(session, player, vendor))
        {
            return false;
        }

        var offered = Offered(player, PricesOf(shop));

        if (offered.Count == 0)
        {
            _speech.Say(vendor, NothingToSell);

            return true;
        }

        _sender.TrySend(
            session.SessionId,
            new VendorSellListPacket(
                vendor.Id,
                offered.Select(pair => new VendorSellListEntry(
                        pair.Item.Id,
                        pair.Item.ItemId,
                        pair.Item.Hue.Value,
                        pair.Item.Amount,
                        pair.Price,
                        NameOf(pair.Item)
                    )
                )
            )
        );
        session.Set(
            VendorSessionKeys.SellWindow,
            new VendorSellWindow(vendor.Id, offered.ToDictionary(pair => pair.Item.Id, pair => pair.Price))
        );

        return true;
    }

    public void Sell(GameSession session, VendorSellReplyPacket packet)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(packet);

        var window = session.Get(VendorSessionKeys.SellWindow);

        if (window is null || packet.Lines.Count >= MaxSellReplyLines)
        {
            return;
        }

        if (window.Vendor != packet.Vendor)
        {
            _sender.TrySend(session.SessionId, new VendorEndPacket(packet.Vendor));

            return;
        }

        if (packet.Lines.Count == 0 ||
            !_mobiles.TryGet(session.CharacterId, out var player) ||
            !_mobiles.TryGet(window.Vendor, out var vendor) ||
            !CanReach(player, vendor) ||
            RefusesMurderer(session, player, vendor) ||
            !TryChosen(player, window, packet, out var chosen))
        {
            EndSell(session, window);

            return;
        }

        var total = chosen.Sum(pair => (long)window.Prices[pair.Key.Id] * pair.Value);

        if (total is < 1 or > int.MaxValue)
        {
            EndSell(session, window);

            return;
        }

        var paid = _bank.GiveGold(player, (int)total);

        if (paid != BankResultType.Ok)
        {
            _speech.TellCliloc(player, paid == BankResultType.Busy ? ClilocOrderCannotBeFulfilled : ClilocBackpackFull);
            EndSell(session, window);

            return;
        }

        foreach (var (item, amount) in chosen)
        {
            if (!_handling.Consume(item, amount))
            {
                _logger.Error("Vendor: {Amount} of {Item} could not be taken after the gold was paid", amount, item.Id);
            }
        }

        EndSell(session, window);
        _fatigue?.LoadChanged(session, player, true);
    }

    public void Close(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Get(VendorSessionKeys.Window) is not null)
        {
            session.Set(VendorSessionKeys.Window, null);
        }

        if (session.Get(VendorSessionKeys.SellWindow) is not null)
        {
            session.Set(VendorSessionKeys.SellWindow, null);
        }
    }

    public void OnSessionClosed(GameSession session)
    {
        Close(session);
    }

    private static string StockKey(ShopLine line)
    {
        return $"{line.Item}|{line.Price}|{line.Hue}";
    }

    private static string NameOf(VendorWindowLine line)
    {
        if (line.Line.Name.Length > 0)
        {
            return line.Line.Name;
        }

        var graphic = (int)line.Template.ItemId.Value;

        return (graphic >= HighGraphic ? HighGraphicCliloc + graphic : GraphicCliloc + (graphic & GraphicMask)).ToString();
    }

    // The stock of vendors that are gone, such as the ones that respawned with another serial.
    private void PruneStock()
    {
        if (_stock.Count <= StockPruneAbove)
        {
            return;
        }

        foreach (var key in _stock.Keys.Where(key => !_mobiles.IsInWorld(key.Vendor)).ToArray())
        {
            _stock.Remove(key);
        }
    }

    private int StockOf(MobileEntity vendor, ShopLine line)
    {
        return _stock.TryGetValue((vendor.Id, StockKey(line)), out var left)
            ? left
            : _stock[(vendor.Id, StockKey(line))] = line.Amount;
    }

    private bool IsStack(Data.Templates.Items.ItemTemplate template)
    {
        return template.EffectiveStackable(_tiles);
    }

    private Serial NextVirtual()
    {
        var serial = new Serial((uint)_nextVirtual);
        // The lower half is the mobile service's, which counts up from the bottom for hair and beards.
        _nextVirtual = _nextVirtual <= VirtualHalf ? Serial.MaxVirtual : _nextVirtual - 1;

        return serial;
    }

    // In the world, alive, on the same map, within ten tiles and in sight.
    private bool CanReach(MobileEntity player, MobileEntity vendor)
    {
        if (player.IsDead || !_mobiles.IsInWorld(vendor.Id) || player.Map != vendor.Map)
        {
            return false;
        }

        var dx = Math.Abs(player.Location.X - vendor.Location.X);
        var dy = Math.Abs(player.Location.Y - vendor.Location.Y);

        if (dx > ViewRange || dy > ViewRange)
        {
            return false;
        }

        try
        {
            return _sight.HasLineOfSight(vendor.Map, vendor.Location, player.Location);
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    // The lines of the reply as the lines of the window, a line sent twice added up, none above its stock.
    private bool TryWanted(
        MobileEntity vendor,
        VendorWindow window,
        VendorBuyReplyPacket packet,
        out Dictionary<VendorWindowLine, int> wanted
    )
    {
        wanted = new();

        foreach (var chosen in packet.Lines)
        {
            if (chosen.Amount < 1 || !window.Lines.TryGetValue(chosen.Item, out var line))
            {
                return false;
            }

            wanted[line] = wanted.GetValueOrDefault(line) + chosen.Amount;
        }

        return wanted.All(pair => pair.Value <= StockOf(vendor, pair.Key.Line));
    }

    private bool TryMake(Dictionary<VendorWindowLine, int> wanted, List<ItemEntity> made)
    {
        foreach (var (line, amount) in wanted)
        {
            var stack = IsStack(line.Template);

            for (var piece = 0; piece < (stack ? 1 : amount); piece++)
            {
                if (_handling.Make(line.Template.Id, stack ? amount : null) is not { } item)
                {
                    _logger.Warning("Vendor: an item of {Template} could not be made", line.Template.Id);

                    return false;
                }

                if (line.Line.Hue != 0)
                {
                    item.Hue = new((ushort)line.Line.Hue);
                }

                made.Add(item);
            }
        }

        return true;
    }

    private static bool IsStaff(GameSession session)
    {
        return session.Get(SessionKeys.AccountType) >= AccountType.GameMaster;
    }

    // Whether the backpack, and from 2000 the bank, hold the price; else the cliloc that says what is lacking.
    private bool CanAfford(MobileEntity player, int pays, out int shortage)
    {
        shortage = ClilocCannotAfford;
        var carried = _bank.CarriedGold(player);

        if (carried >= pays)
        {
            return true;
        }

        if (pays < BankFrom)
        {
            return false;
        }

        shortage = ClilocBankLacksFunds;

        return _bank.Balance(player) is { } balance && balance >= pays - carried;
    }

    // The backpack first. The bank is drawn on only from 2000 gold, for what the backpack lacks.
    private bool TryPay(MobileEntity player, int pays, out bool fromBank, out int refusal)
    {
        fromBank = false;
        refusal = ClilocCannotAfford;

        if (pays == 0)
        {
            return true;
        }

        var carried = _bank.CarriedGold(player);

        if (carried < pays)
        {
            if (pays < BankFrom)
            {
                return false;
            }

            var missing = pays - carried;
            refusal = ClilocBankLacksFunds;

            if (_bank.Balance(player) is not { } balance ||
                balance < missing ||
                _bank.Withdraw(player, (int)missing) != BankResultType.Ok)
            {
                return false;
            }

            fromBank = true;
            refusal = ClilocCannotAfford;
        }

        return _bank.TakeCarriedGold(player, pays);
    }

    // To the backpack when all of it fits, else at the feet of the player.
    private void Deliver(MobileEntity player, List<ItemEntity> made)
    {
        var backpack = _items.GetWorn(player.Id).FirstOrDefault(item => item.Layer == LayerType.Backpack);

        if (backpack is not null && _capacity.HasRoomFor(backpack, made.Count) && _weight.Holds(backpack, made))
        {
            foreach (var item in made)
            {
                item.PutInContainer(
                    backpack.Id,
                    _layouts.RandomGridPosition(backpack.ItemId),
                    ContainerSlotUtils.FirstFree(_items.GetContents(backpack.Id))
                );
                _items.Add([item]);
                _handling.Refresh(item);
            }

            return;
        }

        foreach (var item in made)
        {
            item.PlaceOnGround(player.Map, player.Location);
            _items.Add([item]);
            _view.ItemAppeared(item);
        }
    }

    private bool RefusesMurderer(GameSession session, MobileEntity player, MobileEntity vendor)
    {
        if (!player.IsMurderer || IsStaff(session) || _regions.Find(vendor.Map, vendor.Location)?.Guarded != true)
        {
            return false;
        }

        _speech.SayCliloc(vendor, ClilocMurdererRefused);

        return true;
    }

    // What the shop pays for each item template; the first line of a template counts.
    private static Dictionary<string, int> PricesOf(ShopDefinition shop)
    {
        var prices = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in shop.Sell)
        {
            prices.TryAdd(line.Item, line.Price);
        }

        return prices;
    }

    // The items of the backpack and the bags in it that the shop buys, 250 at most.
    private List<(ItemEntity Item, int Price)> Offered(MobileEntity player, Dictionary<string, int> prices)
    {
        var offered = new List<(ItemEntity, int)>();
        var backpack = _items.GetWorn(player.Id).FirstOrDefault(item => item.Layer == LayerType.Backpack);

        if (backpack is null)
        {
            return offered;
        }

        var pending = new Queue<ItemEntity>([backpack]);

        while (pending.Count > 0 && offered.Count < MaxSellList)
        {
            foreach (var item in _items.GetContents(pending.Dequeue().Id))
            {
                var contents = _items.GetContents(item.Id);

                if (contents.Count > 0)
                {
                    pending.Enqueue(item);
                }
                else if (offered.Count < MaxSellList && IsSellable(item, prices, out var price))
                {
                    offered.Add((item, price));
                }
            }
        }

        return offered;
    }

    private bool IsSellable(ItemEntity item, Dictionary<string, int> prices, out int price)
    {
        price = 0;

        if (item.TemplateId is not { } id || !prices.TryGetValue(id, out price))
        {
            return false;
        }

        var movable = item.Movable ?? (_templates.TryGet(id, out var template) ? template.Movable : null) ?? true;

        // As ModernUO before AOS: only regular loot is bought, so the starting items are not a purse.
        var lootType = item.TryGetProp<LootType>(ItemPropKeys.LootType, out var own) ? own :
            _templates.TryGet(id, out var lootTemplate) ? lootTemplate.EffectiveLootType() : LootType.Regular;

        return movable && lootType == LootType.Regular && item.Amount >= 1 && !_handling.IsHeld(item);
    }

    private string NameOf(ItemEntity item)
    {
        if (item.Name is { Length: > 0 } name)
        {
            return name;
        }

        if (item.TemplateId is { } id)
        {
            if (_templates.TryGet(id, out var template) && template.Name is { Length: > 0 } templateName)
            {
                return templateName;
            }

            // 0x103b_bread_loaf: the words after the graphic.
            var words = id.Contains('_') && id.StartsWith("0x", StringComparison.Ordinal) ? id[(id.IndexOf('_') + 1)..] : id;

            return words.Replace('_', ' ');
        }

        return "";
    }

    // The items of the reply: each one offered, still carried by the player, with its amount cut at what it holds.
    private bool TryChosen(
        MobileEntity player,
        VendorSellWindow window,
        VendorSellReplyPacket packet,
        out Dictionary<ItemEntity, int> chosen
    )
    {
        chosen = new();

        foreach (var line in packet.Lines)
        {
            if (line.Amount < 1 ||
                !window.Prices.ContainsKey(line.Item) ||
                !_items.TryGet(line.Item, out var item) ||
                !IsCarriedBy(item, player) ||
                _handling.IsHeld(item) ||
                _items.GetContents(item.Id).Count > 0)
            {
                return false;
            }

            chosen[item] = Math.Min(item.Amount, chosen.GetValueOrDefault(item) + line.Amount);
        }

        return true;
    }

    // Inside the backpack of the player, at any depth, and not worn.
    private bool IsCarriedBy(ItemEntity item, MobileEntity player)
    {
        var visited = new HashSet<Serial>();

        for (var current = item; current.ContainerId is { } containerId && visited.Add(current.Id);)
        {
            if (!_items.TryGet(containerId, out var container))
            {
                return false;
            }

            if (container.MobileId == player.Id && container.Layer == LayerType.Backpack)
            {
                return true;
            }

            current = container;
        }

        return false;
    }

    private void EndSell(GameSession session, VendorSellWindow window)
    {
        Close(session);
        _sender.TrySend(session.SessionId, new VendorEndPacket(window.Vendor));
    }

    private void Refuse(GameSession session, VendorWindow window, MobileEntity player, int cliloc)
    {
        _speech.TellCliloc(player, cliloc);
        End(session, window);
    }

    private void End(GameSession session, VendorWindow window)
    {
        Close(session);
        _sender.TrySend(session.SessionId, new VendorEndPacket(window.Vendor));
    }
}
