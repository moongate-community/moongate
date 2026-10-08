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

        if (player.IsMurderer && _regions.Find(vendor.Map, vendor.Location)?.Guarded == true)
        {
            _speech.SayCliloc(vendor, ClilocMurdererRefused);

            return false;
        }

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
        _sender.TrySend(id, new WornItemPacket(shopContainer, 0, LayerType.ShopBuy, vendor.Id, 0));
        _sender.TrySend(id, new WornItemPacket(resaleContainer, 0, LayerType.ShopResale, vendor.Id, 0));
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

        if (window is null || window.Vendor != packet.Vendor || packet.Lines.Count > MaxReplyLines)
        {
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

        if (player.IsMurderer && _regions.Find(vendor.Map, vendor.Location)?.Guarded == true)
        {
            _speech.SayCliloc(vendor, ClilocMurdererRefused);
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

        var made = new List<ItemEntity>();
        var pieces = wanted.Sum(pair => IsStack(pair.Key.Template) ? 1 : pair.Value);

        if (pieces > _serials.Available || !TryMake(wanted, made))
        {
            Refuse(session, window, player, ClilocOrderCannotBeFulfilled);

            return;
        }

        var pays = session.Get(SessionKeys.AccountType) >= AccountType.GameMaster ? 0 : (int)total;

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

    public void Close(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.Get(VendorSessionKeys.Window) is not null)
        {
            session.Set(VendorSessionKeys.Window, null);
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
        return line.Line.Name.Length > 0
            ? line.Line.Name
            : (GraphicCliloc + ((int)line.Template.ItemId.Value & GraphicMask)).ToString();
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
        _nextVirtual = _nextVirtual == Serial.MinVirtual ? Serial.MaxVirtual : _nextVirtual - 1;

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
