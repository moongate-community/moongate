using Moongate.Server.Ultima.Interfaces.Items;
using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Internal.Bank;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Speech;
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
                });
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
