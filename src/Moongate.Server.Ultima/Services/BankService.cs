using Moongate.Server.Ultima.Interfaces.Items;
using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Bank;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Speech;
using Moongate.Server.Ultima.Types.Bank;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     ModernUO's bank box: a container worn on the bank layer, shown with the equip update (0x2E), the container gump
///     (0x24) and its contents (0x3C), with a line of how many items it holds.
/// </summary>
public sealed class BankService : IBankService
{
    public const string BankTemplate = "bank_box";

    /// <summary>
    ///     The item template of a bank check.
    /// </summary>
    public const string CheckTemplate = "bank_check";

    /// <summary>
    ///     The client text that names a check: "A bank check".
    /// </summary>
    public const int CheckLabel = 1041361;

    /// <summary>
    ///     The most coins of one pile.
    /// </summary>
    public const int PileMaximum = 60_000;

    /// <summary>
    ///     The most piles one cashing of a check makes; the check keeps what is beyond them.
    /// </summary>
    public const int CashPilesMaximum = 125;

    private const int GoldItemId = 0x0EED;
    private static readonly Hue MessageHue = new(0x03B2);
    private static readonly TimeSpan ShowAgainAfter = TimeSpan.FromSeconds(1);

    private readonly ILogger _logger = Log.ForContext<BankService>();
    private readonly ConcurrentDictionary<Serial, OpenBank> _open = new();
    private readonly ConcurrentDictionary<Serial, byte> _making = new();
    private readonly IItemService _items;
    private readonly IItemFactoryService _factory;
    private readonly ISessionService _sessions;
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;
    private readonly ITooltipService _tooltips;
    private readonly IContainerLayoutService _layouts;
    private readonly IGameLoopService _loop;
    private readonly IItemHandlingService _handling;
    private readonly IContainerCapacityService _capacity;
    private readonly IWeightService _weight;
    private readonly ItemsConfig _itemsConfig;
    private readonly BankConfig _config;
    private readonly IFatigueService? _fatigue;
    private readonly ILocalizationService? _localization;
    private readonly TimeProvider _time;
    private readonly IInventoryMutationGuard? _inventory;
    private readonly IInventoryReservationService? _reservations;

    public BankService(
        IItemService items,
        IItemFactoryService factory,
        ISessionService sessions,
        IMobileService mobiles,
        IPacketSendService sender,
        ITooltipService tooltips,
        IContainerLayoutService layouts,
        IGameLoopService loop,
        IItemHandlingService handling,
        IContainerCapacityService capacity,
        IWeightService weight,
        ItemsConfig itemsConfig,
        BankConfig config,
        IFatigueService? fatigue = null,
        ILocalizationService? localization = null,
        TimeProvider? time = null,
        IInventoryMutationGuard? inventory = null,
        IInventoryReservationService? reservations = null
    )
    {
        _inventory = inventory;
        _reservations = reservations;
        _time = time ?? TimeProvider.System;
        _items = items;
        _factory = factory;
        _sessions = sessions;
        _mobiles = mobiles;
        _sender = sender;
        _tooltips = tooltips;
        _layouts = layouts;
        _loop = loop;
        _handling = handling;
        _capacity = capacity;
        _weight = weight;
        _itemsConfig = itemsConfig;
        _config = config;
        _fatigue = fatigue;
        _localization = localization;
    }

    public bool Open(MobileEntity player)
    {
        if (_inventory?.AllowsOwner(player.Id) == false || !_sessions.TryGetByCharacterId(player.Id, out var session))
        {
            return false;
        }

        if (BoxOf(player.Id) is { } box)
        {
            // Every banker in range hears the same word: the bank shows once.
            if (!IsOpen(player) || _time.GetUtcNow() - _open[player.Id].At >= ShowAgainAfter)
            {
                Show(player, session, box);
            }

            return true;
        }

        if (_making.TryAdd(player.Id, 0))
        {
            _ = MakeAsync(player);
        }

        return true;
    }

    public void Close(MobileEntity player)
    {
        _open.TryRemove(player.Id, out _);
    }

    public void OnSessionClosed(GameSession session)
    {
        _open.TryRemove(session.CharacterId, out _);
    }

    public bool IsOpen(MobileEntity player)
    {
        return _open.TryGetValue(player.Id, out var open) &&
               ReferenceEquals(open.Player, player) &&
               open.Map == player.Map &&
               open.Location == player.Location;
    }

    public bool CanAccess(GameSession session, MobileEntity character, ItemEntity item)
    {
        if (_items.GetWornRoot(item) is not { Layer: LayerType.Bank } box)
        {
            return true;
        }

        return session.AccountType >= AccountType.GameMaster || box.MobileId == character.Id && IsOpen(character);
    }

    public int? Balance(MobileEntity player)
    {
        if (player.IsNpc)
        {
            return null;
        }

        if (BoxOf(player.Id) is not { } box)
        {
            return 0;
        }

        var gold = GoldIn(box).Sum(pile => (long)pile.Amount) + ChecksIn(box).Sum(check => WorthOf(check) ?? 0);

        return (int)Math.Min(gold, int.MaxValue);
    }

    public BankResultType Withdraw(MobileEntity player, int amount)
    {
        if (_inventory?.AllowsOwner(player.Id) == false)
        {
            return BankResultType.Busy;
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return BankResultType.NoPlayer;
        }

        if (amount < 1)
        {
            return BankResultType.BadAmount;
        }

        if (amount > _config.MaxWithdraw)
        {
            return BankResultType.TooMuch;
        }

        if (BoxOf(player.Id) is not { } box)
        {
            return BankResultType.NoBank;
        }

        var piles = GoldIn(box);
        var checks = ChecksIn(box);

        // The coins first; when they are not enough the checks give the rest.
        if (piles.Sum(pile => (long)pile.Amount) + checks.Sum(check => WorthOf(check) ?? 0) < amount)
        {
            return BankResultType.NotEnoughGold;
        }

        // As ModernUO: a backpack that is already at its weight takes nothing, any other takes the gold whatever it
        // weighs, and its owner walks away overloaded. Sixty thousand coins weigh more than a backpack holds.
        if (BackpackOf(player.Id) is not { } backpack || !_weight.Holds(backpack, Coins(1)))
        {
            return BankResultType.BackpackFull;
        }

        // The gold joins a pile of the backpack that has the room; else it is a pile of its own, which needs a place.
        var onto = TopPilesOf(backpack).FirstOrDefault(pile => pile.Amount + amount <= PileMaximum);
        ItemEntity? made = null;

        if (onto is null)
        {
            if (!_capacity.HasRoomFor(backpack, 1))
            {
                return BankResultType.BackpackFull;
            }

            if ((made = _handling.Make(_itemsConfig.GoldTemplate, amount)) is null)
            {
                return BankResultType.Busy;
            }
        }

        // Nothing can refuse from here on.
        TakeFromChecks(checks, Take(piles, amount));

        if (onto is not null)
        {
            onto.Amount += amount;
            _handling.Refresh(onto);
        }
        else
        {
            // Safe: made is created whenever no existing stack was topped up.
            Put(made!, backpack);
        }

        LoadChanged(player);

        return BankResultType.Ok;
    }

    public BankResultType Deposit(MobileEntity player, int amount)
    {
        if (_inventory?.AllowsOwner(player.Id) == false)
        {
            return BankResultType.Busy;
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return BankResultType.NoPlayer;
        }

        if (amount < 1)
        {
            return BankResultType.BadAmount;
        }

        if (BoxOf(player.Id) is not { } box)
        {
            return BankResultType.NoBank;
        }

        var carried = BackpackOf(player.Id) is { } backpack ? GoldIn(backpack) : [];

        if (carried.Sum(pile => (long)pile.Amount) < amount)
        {
            return BankResultType.NotEnoughGold;
        }

        // The piles of the box are topped up first; what is left makes piles of its own, which need their places.
        var there = TopPilesOf(box);
        var left = amount - Math.Min(amount, there.Sum(pile => PileMaximum - pile.Amount));
        var newPiles = (left + PileMaximum - 1) / PileMaximum;

        if (!_capacity.HasRoomFor(box, newPiles))
        {
            return BankResultType.BankFull;
        }

        var made = new List<ItemEntity>(newPiles);

        for (var remaining = left; remaining > 0; remaining -= PileMaximum)
        {
            if (_handling.Make(_itemsConfig.GoldTemplate, Math.Min(remaining, PileMaximum)) is not { } pile)
            {
                return BankResultType.Busy;
            }

            made.Add(pile);
        }

        // Nothing can refuse from here on.
        Take(carried, amount);
        var toTopUp = amount - left;

        foreach (var pile in there)
        {
            if (toTopUp == 0)
            {
                break;
            }

            var added = Math.Min(toTopUp, PileMaximum - pile.Amount);

            if (added > 0)
            {
                pile.Amount += added;
                toTopUp -= added;
                _handling.Refresh(pile);
            }
        }

        foreach (var pile in made)
        {
            Put(pile, box);
        }

        LoadChanged(player);

        return BankResultType.Ok;
    }

    public BankResultType DepositItem(MobileEntity player, ItemEntity item)
    {
        if (_inventory?.AllowsOwner(player.Id) == false)
        {
            return BankResultType.Busy;
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return BankResultType.NoPlayer;
        }

        var isGold = item.TemplateId == _itemsConfig.GoldTemplate;
        var worth = isGold ? item.Amount : WorthOf(item);

        // Gold or a check, the player's own or nobody's, and on no cursor.
        if (worth is not > 0 ||
            item.MobileId is not null ||
            _handling.IsHeld(item) ||
            (_items.GetOwner(item) is { } owner && owner != player.Id))
        {
            return BankResultType.NotMoney;
        }

        if (BoxOf(player.Id) is not { } box)
        {
            return BankResultType.NoBank;
        }

        if (IsInside(item, box))
        {
            return BankResultType.Ok;
        }

        // Gold tops up the piles of the box first, and what is left is a pile of its own; a check is always one item.
        var there = isGold ? TopPilesOf(box) : [];
        var toTopUp = isGold ? (int)Math.Min(item.Amount, there.Sum(pile => (long)(PileMaximum - pile.Amount))) : 0;
        var left = isGold ? item.Amount - toTopUp : 0;
        var needsAPlace = !isGold || left > 0;

        if (needsAPlace && !_capacity.HasRoomFor(box, 1))
        {
            return BankResultType.BankFull;
        }

        // What arrives in the box is made anew and what was handed over is taken: the paths of a deposit by speech.
        ItemEntity? made = null;

        if (needsAPlace &&
            (made = isGold ? _handling.Make(_itemsConfig.GoldTemplate, left) : _handling.Make(CheckTemplate)) is null)
        {
            return BankResultType.Busy;
        }

        if (!(isGold ? _handling.Consume(item, item.Amount) : _handling.Delete(item)))
        {
            return BankResultType.Busy;
        }

        // Nothing can refuse from here on.
        foreach (var pile in there)
        {
            if (toTopUp == 0)
            {
                break;
            }

            var added = Math.Min(toTopUp, PileMaximum - pile.Amount);
            pile.Amount += added;
            toTopUp -= added;
            _handling.Refresh(pile);
        }

        if (made is not null)
        {
            if (!isGold)
            {
                made.SetProp(ItemPropKeys.BankWorth, worth.Value);
                made.SetProp(ItemPropKeys.LabelNumber, (long)CheckLabel);
            }

            Put(made, box);
        }

        LoadChanged(player);

        return BankResultType.Ok;
    }

    public BankResultType GiveGold(MobileEntity player, int amount)
    {
        if (_inventory?.AllowsOwner(player.Id) == false)
        {
            return BankResultType.Busy;
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return BankResultType.NoPlayer;
        }

        if (amount < 1)
        {
            return BankResultType.BadAmount;
        }

        var piles = (amount + PileMaximum - 1) / PileMaximum;
        var target = new[] { BackpackOf(player.Id), BoxOf(player.Id) }.FirstOrDefault(container =>
            container is not null && _capacity.HasRoomFor(container, piles)
        );

        if (target is null)
        {
            return BankResultType.BackpackFull;
        }

        var made = new List<ItemEntity>(piles);

        for (var remaining = amount; remaining > 0; remaining -= PileMaximum)
        {
            if (_handling.Make(_itemsConfig.GoldTemplate, Math.Min(remaining, PileMaximum)) is not { } pile)
            {
                return BankResultType.Busy;
            }

            made.Add(pile);
        }

        foreach (var pile in made)
        {
            Put(pile, target);
        }

        LoadChanged(player);

        return BankResultType.Ok;
    }

    public long CarriedGold(MobileEntity player)
    {
        return BackpackOf(player.Id) is { } backpack ? GoldIn(backpack).Sum(pile => (long)pile.Amount) : 0;
    }

    public BankResultType CanPay(MobileEntity player, int amount, bool useBank)
    {
        return Plan(player, amount, useBank).Result;
    }

    public BankResultType Pay(MobileEntity player, int amount, bool useBank, out int fromBank)
    {
        fromBank = 0;
        var (result, pack, coins, checks, inPack, inCoins) = Plan(player, amount, useBank);

        if (result != BankResultType.Ok)
        {
            return result;
        }

        var fromPack = (int)Math.Min(amount, inPack);
        var owed = amount - fromPack;
        var fromCoins = Math.Min(owed, (int)Math.Min(inCoins, int.MaxValue));
        var left = Take(pack, fromPack) + Take(coins, fromCoins);
        TakeFromChecks(checks, owed - fromCoins);
        fromBank = owed;

        if (left > 0)
        {
            // The coins were checked above, so this is a fault; the player has paid the less.
            _logger.Error(
                "Bank: {Left} of {Amount} coins could not be taken for a payment of {Player}",
                left,
                amount,
                player.Id
            );
        }

        LoadChanged(player);

        return BankResultType.Ok;
    }

    public long? WorthOf(ItemEntity item)
    {
        return CheckWorth(item);
    }

    /// <summary>
    ///     Gets what a bank check is worth; null for an item that is not one. Only a check is a check: the prop on
    ///     anything else is worth nothing, and so is a worth that is not a number above 0.
    /// </summary>
    public static long? CheckWorth(ItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.TemplateId != CheckTemplate)
        {
            return null;
        }

        try
        {
            return item.TryGetProp<long>(ItemPropKeys.BankWorth, out var worth) && worth > 0 ? worth : null;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    public BankResultType WriteCheck(MobileEntity player, int amount)
    {
        if (_inventory?.AllowsOwner(player.Id) == false)
        {
            return BankResultType.Busy;
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return BankResultType.NoPlayer;
        }

        if (amount < 1)
        {
            return BankResultType.BadAmount;
        }

        if (amount < _config.MinCheck)
        {
            return BankResultType.CheckTooSmall;
        }

        if (amount > _config.MaxCheck)
        {
            return BankResultType.CheckTooBig;
        }

        if (BoxOf(player.Id) is not { } box)
        {
            return BankResultType.NoBank;
        }

        // A check is paid with coins: the gold is counted before the room, so a full box does not hide a short balance.
        var piles = GoldIn(box);

        if (piles.Sum(pile => (long)pile.Amount) < amount)
        {
            return BankResultType.NotEnoughGold;
        }

        // A pile the check uses up leaves its place to the check.
        var left = amount;
        var freed = 0;

        foreach (var pile in piles)
        {
            if (left < pile.Amount)
            {
                break;
            }

            left -= pile.Amount;
            freed++;
        }

        if (freed == 0 && !_capacity.HasRoomFor(box, 1))
        {
            return BankResultType.BankFull;
        }

        if (_handling.Make(CheckTemplate) is not { } check)
        {
            return BankResultType.Busy;
        }

        // Nothing can refuse from here on.
        Take(piles, amount);
        check.SetProp(ItemPropKeys.BankWorth, (long)amount);
        check.SetProp(ItemPropKeys.LabelNumber, (long)CheckLabel);
        Put(check, box);

        return BankResultType.Ok;
    }

    public BankResultType Cash(MobileEntity player, ItemEntity check, out int deposited)
    {
        deposited = 0;

        if (_inventory?.AllowsOwner(player.Id) == false || _inventory?.Allows(check) == false)
        {
            return BankResultType.Busy;
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return BankResultType.NoPlayer;
        }

        if (BoxOf(player.Id) is not { } box)
        {
            return BankResultType.NoBank;
        }

        if (WorthOf(check) is not { } worth || !IsInside(check, box) || _handling.IsHeld(check))
        {
            return BankResultType.NotInBank;
        }

        // The piles of the box are topped up first; what is left makes piles of its own.
        var there = TopPilesOf(box);
        var topUp = (int)Math.Min(worth, there.Sum(pile => (long)(PileMaximum - pile.Amount)));
        var left = worth - topUp;
        var wanted = (int)Math.Min(left / PileMaximum + (left % PileMaximum > 0 ? 1 : 0), CashPilesMaximum + 1);

        // Cashed whole, the check leaves its place to one of the piles. With room for fewer piles the box takes what
        // fits and the check keeps the rest; so it does beyond the piles of one cashing, which a box with no limit
        // would otherwise take by the thousand in one turn of the loop.
        var whole = wanted <= CashPilesMaximum && _capacity.HasRoomFor(box, Math.Max(0, wanted - 1));
        var newPiles = wanted;

        if (!whole)
        {
            for (newPiles = Math.Min(wanted - 1, CashPilesMaximum);
                 newPiles > 0 && !_capacity.HasRoomFor(box, newPiles);
                 newPiles--)
            {
            }
        }

        var going = whole ? worth : topUp + (long)newPiles * PileMaximum;

        if (going == 0)
        {
            return BankResultType.BankFull;
        }

        var made = new List<ItemEntity>(newPiles);

        for (var remaining = going - topUp; remaining > 0; remaining -= PileMaximum)
        {
            if (_handling.Make(_itemsConfig.GoldTemplate, (int)Math.Min(remaining, PileMaximum)) is not { } pile)
            {
                return BankResultType.Busy;
            }

            made.Add(pile);
        }

        // Nothing can refuse from here on. The check goes first: its place is the one a pile takes.
        if (whole)
        {
            Remove(check);
        }
        else
        {
            check.SetProp(ItemPropKeys.BankWorth, worth - going);
            _handling.Refresh(check);
        }

        foreach (var pile in there)
        {
            if (topUp == 0)
            {
                break;
            }

            var added = Math.Min(topUp, PileMaximum - pile.Amount);

            if (added > 0)
            {
                pile.Amount += added;
                topUp -= added;
                _handling.Refresh(pile);
            }
        }

        foreach (var pile in made)
        {
            Put(pile, box);
        }

        deposited = (int)Math.Min(going, int.MaxValue);

        return BankResultType.Ok;
    }

    // Gold weighs: the player's status shows its new load, and it is warned when it is now overloaded.
    private void LoadChanged(MobileEntity player)
    {
        if (_fatigue is not null && _sessions.TryGetByCharacterId(player.Id, out var session))
        {
            _fatigue.LoadChanged(session, player, true);
        }
    }

    // The gold piles inside a container, at any depth, the smallest first: taking from them empties the small ones.
    // A pile on a cursor is not there to take.
    private List<ItemEntity> GoldIn(ItemEntity container)
    {
        var piles = new List<ItemEntity>();
        var visited = new HashSet<Serial>();
        Collect(container);

        return piles.OrderBy(pile => pile.Amount).ThenBy(pile => pile.Id.Value).ToList();

        void Collect(ItemEntity inside)
        {
            if (!visited.Add(inside.Id))
            {
                return;
            }

            foreach (var item in _items.GetContents(inside.Id))
            {
                if (item.TemplateId == _itemsConfig.GoldTemplate)
                {
                    if (!_handling.IsHeld(item))
                    {
                        piles.Add(item);
                    }
                }
                else
                {
                    Collect(item);
                }
            }
        }
    }

    // The gold piles lying in the container itself, the fullest first: gold joins them before it makes a new pile.
    private List<ItemEntity> TopPilesOf(ItemEntity container)
    {
        return _items.GetContents(container.Id)
            .Where(item => item.TemplateId == _itemsConfig.GoldTemplate && item.Amount < PileMaximum &&
                           !_handling.IsHeld(item)
            )
            .OrderByDescending(item => item.Amount)
            .ThenBy(item => item.Id.Value)
            .ToList();
    }

    // Takes the coins pile by pile; what the piles could not give.
    private int Take(List<ItemEntity> piles, int amount)
    {
        foreach (var pile in piles)
        {
            if (amount == 0)
            {
                break;
            }

            var taken = Math.Min(amount, pile.Amount);

            if (_handling.Consume(pile, taken))
            {
                amount -= taken;
            }
            else
            {
                // Its callers counted these coins as theirs to take: gold may have been made out of nothing.
                _logger.Error("Bank: {Amount} coins could not be taken from the gold pile {Item}", taken, pile.Id);
            }
        }

        return amount;
    }

    // A check that gave all its gold. Nothing refuses it today; if something ever does, the gold was paid twice.
    private void Remove(ItemEntity check)
    {
        if (!_handling.Delete(check))
        {
            _logger.Error("Bank: the used up check {Item} could not be removed", check.Id);
        }
    }

    // Takes the rest from the checks, the smallest first: one used up is gone, the last one keeps what is left.
    private void TakeFromChecks(List<ItemEntity> checks, int amount)
    {
        foreach (var check in checks)
        {
            if (amount == 0)
            {
                break;
            }

            var worth = WorthOf(check) ?? 0;
            var taken = (int)Math.Min(amount, worth);
            amount -= taken;

            if (taken == worth)
            {
                Remove(check);
            }
            else
            {
                check.SetProp(ItemPropKeys.BankWorth, worth - taken);
                _handling.Refresh(check);
            }
        }
    }

    // The checks inside a container, at any depth, the smallest first. One on a cursor is not there to take.
    private List<ItemEntity> ChecksIn(ItemEntity container)
    {
        var checks = new List<ItemEntity>();
        var visited = new HashSet<Serial>();
        Collect(container);

        return checks.OrderBy(check => WorthOf(check)).ThenBy(check => check.Id.Value).ToList();

        void Collect(ItemEntity inside)
        {
            if (!visited.Add(inside.Id))
            {
                return;
            }

            foreach (var item in _items.GetContents(inside.Id))
            {
                if (WorthOf(item) is not null)
                {
                    if (!_handling.IsHeld(item))
                    {
                        checks.Add(item);
                    }
                }
                else
                {
                    Collect(item);
                }
            }
        }
    }

    // Whether the item lies inside the container, at any depth.
    private bool IsInside(ItemEntity item, ItemEntity container)
    {
        var visited = new HashSet<Serial>();

        for (var current = item; current.ContainerId is { } parent && visited.Add(current.Id);)
        {
            if (parent == container.Id)
            {
                return true;
            }

            // Safe: the out value is only used when the lookup succeeds.
            if (!_items.TryGet(parent, out current!))
            {
                return false;
            }
        }

        return false;
    }

    private void Put(ItemEntity pile, ItemEntity container)
    {
        pile.PutInContainer(
            container.Id,
            _layouts.RandomGridPosition(container.ItemId),
            ContainerSlotUtils.FirstFree(_items.GetContents(container.Id))
        );
        _items.Add([pile]);
        _handling.Refresh(pile);
    }

    // That many coins as an item nobody holds yet, to ask a container whether it still holds anything.
    private ItemEntity Coins(int amount)
    {
        return new() { TemplateId = _itemsConfig.GoldTemplate, ItemId = GoldItemId, Amount = amount };
    }

    // What a payment would take, checked whole: the piles of the backpack, the coins and the checks of the box (only when
    // the bank may be used), and how much the backpack and the coins hold. Nothing moves.
    private (BankResultType Result, List<ItemEntity> Pack, List<ItemEntity> Coins, List<ItemEntity> Checks, long InPack, long InCoins) Plan(
        MobileEntity player,
        int amount,
        bool useBank
    )
    {
        List<ItemEntity> none = [];

        if (_inventory?.AllowsOwner(player.Id) == false)
        {
            return (BankResultType.Busy, none, none, none, 0, 0);
        }

        if (player.IsNpc || !_mobiles.TryGet(player.Id, out _))
        {
            return (BankResultType.NoPlayer, none, none, none, 0, 0);
        }

        if (amount < 1)
        {
            return (BankResultType.BadAmount, none, none, none, 0, 0);
        }

        var pack = BackpackOf(player.Id) is { } backpack ? GoldIn(backpack) : none;
        var box = useBank ? BoxOf(player.Id) : null;
        var coins = box is null ? none : GoldIn(box);
        var checks = box is null ? none : ChecksIn(box);
        var inPack = pack.Sum(pile => (long)pile.Amount);
        var inCoins = coins.Sum(pile => (long)pile.Amount);
        var inChecks = checks.Sum(check => WorthOf(check) ?? 0);

        return inPack + inCoins + inChecks < amount
            ? (BankResultType.NotEnoughGold, none, none, none, 0, 0)
            : (BankResultType.Ok, pack, coins, checks, inPack, inCoins);
    }

    private ItemEntity? BackpackOf(Serial player)
    {
        return _items.GetWorn(player).FirstOrDefault(item => item.Layer == LayerType.Backpack);
    }

    private ItemEntity? BoxOf(Serial player)
    {
        return _items.GetWorn(player).FirstOrDefault(item => item.Layer == LayerType.Bank);
    }

    // Saved first, off the loop: the database gives the box its serial. Then live and shown on the loop, only if the
    // same character is still in the world: otherwise its next login loads the saved box.
    private async Task MakeAsync(MobileEntity player)
    {
        try
        {
            var box = _factory.Create(BankTemplate);
            box.Equip(player.Id, LayerType.Bank);
            await _factory.SaveAsync(box);

            var applied = false;
            while (!applied)
            {
                Task? settlement = null;
                var work = new LoopActionWorkItem(() =>
                    {
                        if (_inventory?.AllowsOwner(player.Id) == false)
                        {
                            // Safe: AddBookAttachments registers the guard and the reservations together,
                            // so a guard that refuses comes with a reservation service.
                            settlement = _reservations!.WaitAsync(player.Id);
                            return;
                        }

                        applied = true;
                        _making.TryRemove(player.Id, out _);
                        if (_mobiles.TryGet(player.Id, out var live) && ReferenceEquals(live, player) &&
                            _sessions.TryGetByCharacterId(player.Id, out var session))
                        {
                            _items.Add([box]);
                            Show(player, session, box);
                        }
                    }
                );
                if (_loop.IsOnLoopThread)
                {
                    if (!_loop.TryPost(work)) throw new InvalidOperationException("The loop refused bank application.");
                }
                else
                {
                    await _loop.PostAsync(work);
                }

                await work.Completion;
                if (settlement is not null) await settlement;
            }
        }
        catch (Exception exception)
        {
            _making.TryRemove(player.Id, out _);
            _logger.Error(exception, "Making the bank box of {Player} failed", player);
        }
    }

    private void Show(MobileEntity player, GameSession session, ItemEntity box)
    {
        _open[player.Id] = new(player, player.Map, player.Location, _time.GetUtcNow());
        var contents = _items.GetContents(box.Id);

        _sender.TrySend(session.SessionId, new WornItemPacket(box));
        _sender.TrySend(
            session.SessionId,
            new DisplayContainerPacket(box.Id, _layouts.GetLayout(box.ItemId).Gump, session.UsesHighSeasContainers())
        );
        _sender.TrySend(session.SessionId, new ContainerContentPacket(contents, session.UsesContainerGrid()));

        foreach (var content in contents)
        {
            _sender.TrySend(session.SessionId, _tooltips.Info(content));
        }

        var text = _localization.Text(CommandMessages.BankContents, "Bank container has {0} items.", contents.Count);
        SpeechMessageHelper.TrySend(_sender, session, SpeechMessageHelper.CreateSystem(text, MessageHue));
    }
}
