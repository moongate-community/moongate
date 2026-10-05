using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal.Bank;
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
    ///     The most coins of one pile.
    /// </summary>
    public const int PileMaximum = 60_000;

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
        TimeProvider? time = null
    )
    {
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
        if (!_sessions.TryGetByCharacterId(player.Id, out var session))
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

        return BoxOf(player.Id) is { } box ? (int)Math.Min(GoldIn(box).Sum(pile => (long)pile.Amount), int.MaxValue) : 0;
    }

    public BankResultType Withdraw(MobileEntity player, int amount)
    {
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

        if (piles.Sum(pile => (long)pile.Amount) < amount)
        {
            return BankResultType.NotEnoughGold;
        }

        if (BackpackOf(player.Id) is not { } backpack || !_weight.Holds(backpack, Coins(amount)))
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
        Take(piles, amount);

        if (onto is not null)
        {
            onto.Amount += amount;
            _handling.Refresh(onto);
        }
        else
        {
            Put(made!, backpack);
        }

        LoadChanged(player);

        return BankResultType.Ok;
    }

    public BankResultType Deposit(MobileEntity player, int amount)
    {
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
                     .Where(item => item.TemplateId == _itemsConfig.GoldTemplate && item.Amount < PileMaximum && !_handling.IsHeld(item))
                     .OrderByDescending(item => item.Amount)
                     .ThenBy(item => item.Id.Value)
                     .ToList();
    }

    private void Take(List<ItemEntity> piles, int amount)
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
        }
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

    // That many coins as an item nobody holds yet, to ask a container whether it holds their weight.
    private ItemEntity Coins(int amount)
    {
        return new() { TemplateId = _itemsConfig.GoldTemplate, ItemId = 0x0EED, Amount = amount };
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

            var posted = _loop.TryPost(
                new LoopActionWorkItem(
                    () =>
                    {
                        _making.TryRemove(player.Id, out _);

                        if (_mobiles.TryGet(player.Id, out var live) &&
                            ReferenceEquals(live, player) &&
                            _sessions.TryGetByCharacterId(player.Id, out var session))
                        {
                            _items.Add([box]);
                            Show(player, session, box);
                        }
                    }
                )
            );

            if (!posted)
            {
                _making.TryRemove(player.Id, out _);
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
        _sender.TrySend(session.SessionId, new DisplayContainerPacket(box.Id, _layouts.GetLayout(box.ItemId).Gump, session.UsesHighSeasContainers()));
        _sender.TrySend(session.SessionId, new ContainerContentPacket(contents, session.UsesContainerGrid()));

        foreach (var content in contents)
        {
            _sender.TrySend(session.SessionId, _tooltips.Info(content));
        }

        var text = _localization.Text(CommandMessages.BankContents, "Bank container has {0} items.", contents.Count);
        SpeechMessageHelper.TrySend(_sender, session, SpeechMessageHelper.CreateSystem(text, MessageHue));
    }
}
