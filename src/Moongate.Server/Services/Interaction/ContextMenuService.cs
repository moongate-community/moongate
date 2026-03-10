using System.Collections.Concurrent;
using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Types.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Version;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Handles client context menu request/selection flow (0xBF/0x13, 0x14, 0x15).
/// </summary>
[RegisterGameEventListener]
public sealed class ContextMenuService
    : IContextMenuService,
      IGameEventListener<ContextMenuRequestedEvent>,
      IGameEventListener<ContextMenuEntrySelectedEvent>,
      IMoongateService
{
    private const string SellProfileIdKey = "sell_profile_id";
    private const int ContextMenuInteractionRange = 18;

    private const ushort PaperdollEntryTag = 1;
    private const ushort VendorBuyEntryTag = 2;
    private const ushort VendorSellEntryTag = 3;
    private const ushort ScriptEntryStartTag = 1000;

    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IMobileService _mobileService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly ILuaBrainRunner? _luaBrainRunner;

    private readonly ConcurrentDictionary<long, PendingContextMenuState> _pendingMenus = new();

    public ContextMenuService(
        IGameNetworkSessionService gameNetworkSessionService,
        IMobileService mobileService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameEventBusService gameEventBusService,
        ILuaBrainRunner? luaBrainRunner = null
    )
    {
        _gameNetworkSessionService = gameNetworkSessionService;
        _mobileService = mobileService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameEventBusService = gameEventBusService;
        _luaBrainRunner = luaBrainRunner;
    }

    public async Task HandleAsync(ContextMenuRequestedEvent gameEvent, CancellationToken cancellationToken = default)
        => await SendContextMenuAsync(gameEvent.SessionId, gameEvent.TargetSerial, cancellationToken);

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;

    public async Task HandleAsync(
        ContextMenuEntrySelectedEvent gameEvent,
        CancellationToken cancellationToken = default
    )
    {
        if (!_pendingMenus.TryRemove(gameEvent.SessionId, out var pending))
        {
            return;
        }

        if (pending.TargetSerial != gameEvent.TargetSerial)
        {
            return;
        }

        if (!pending.EntryActions.TryGetValue(gameEvent.EntryTag, out var action))
        {
            return;
        }

        if (!_gameNetworkSessionService.TryGet(gameEvent.SessionId, out var session))
        {
            return;
        }

        var targetMobile = await ResolveTargetMobileAsync(session, gameEvent.TargetSerial, cancellationToken);

        if (targetMobile is null)
        {
            return;
        }

        if (!CanInteractWithTarget(session, targetMobile))
        {
            return;
        }

        if (action.Action != ContextMenuActionType.OpenPaperdoll)
        {
            await PublishActionAsync(action, gameEvent.SessionId, targetMobile, session);

            return;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, new PaperdollPacket(targetMobile));
    }

    public async Task<bool> SendContextMenuAsync(
        long sessionId,
        Serial targetSerial,
        CancellationToken cancellationToken = default
    )
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out var session))
        {
            return false;
        }

        if (!SupportsContextMenu(session))
        {
            return false;
        }

        var targetMobile = await ResolveTargetMobileAsync(session, targetSerial, cancellationToken);

        if (targetMobile is null)
        {
            return false;
        }

        if (!CanInteractWithTarget(session, targetMobile))
        {
            return false;
        }

        var entries = BuildEntries(targetMobile);
        var entryActions =
            new Dictionary<ushort, (ContextMenuActionType Action, string? ScriptKey)>(capacity: entries.Count);

        if (_luaBrainRunner is not null)
        {
            var customEntries = _luaBrainRunner.GetContextMenuEntries(targetMobile, session.Character);
            var nextTag = ScriptEntryStartTag;

            foreach (var customEntry in customEntries)
            {
                if (string.IsNullOrWhiteSpace(customEntry.Key) || string.IsNullOrWhiteSpace(customEntry.Text))
                {
                    continue;
                }

                while (entryActions.ContainsKey((ushort)nextTag))
                {
                    nextTag++;
                }

                if (nextTag > ushort.MaxValue)
                {
                    break;
                }

                var tag = (ushort)nextTag++;
                entries.Add(new(tag, 3_001_000, Hue: 0x0481));
                entryActions[tag] = (ContextMenuActionType.Script, customEntry.Key);
            }
        }

        if (entries.Count == 0)
        {
            return false;
        }

        var packet = GeneralInformationPacketBuilder.CreateDisplayPopupContextMenu2D((uint)targetSerial, entries);
        _outgoingPacketQueue.Enqueue(sessionId, packet);

        foreach (var entry in entries)
        {
            if (entryActions.ContainsKey(entry.EntryTag))
            {
                continue;
            }

            entryActions[entry.EntryTag] = entry.EntryTag switch
            {
                PaperdollEntryTag  => (ContextMenuActionType.OpenPaperdoll, null),
                VendorBuyEntryTag  => (ContextMenuActionType.VendorBuy, null),
                VendorSellEntryTag => (ContextMenuActionType.VendorSell, null),
                _                  => (ContextMenuActionType.None, null)
            };
        }

        _pendingMenus[sessionId] = new(targetSerial, entryActions);

        return true;
    }

    private static bool SupportsContextMenu(Data.Session.GameSession session)
    {
        var clientVersion = session.ClientVersion;

        if (clientVersion is null)
        {
            return false;
        }

        return clientVersion.ProtocolChanges.HasFlag(ProtocolChanges.StygianAbyss);
    }

    private static bool CanInteractWithTarget(Data.Session.GameSession session, UOMobileEntity targetMobile)
    {
        if (session.AccountType >= AccountType.GameMaster)
        {
            return true;
        }

        if (session.Character is null)
        {
            return false;
        }

        if (session.Character.MapId != targetMobile.MapId)
        {
            return false;
        }

        return session.Character.Location.InRange(targetMobile.Location, ContextMenuInteractionRange);
    }

    private async ValueTask PublishActionAsync(
        (ContextMenuActionType Action, string? ScriptKey) action,
        long sessionId,
        UOMobileEntity targetMobile,
        Data.Session.GameSession session
    )
    {
        if (action.Action == ContextMenuActionType.Script && !string.IsNullOrWhiteSpace(action.ScriptKey))
        {
            if (_luaBrainRunner is not null &&
                _luaBrainRunner.TryHandleContextMenuSelection(targetMobile, session.Character, action.ScriptKey, sessionId))
            {
                return;
            }
        }

        await PublishVendorActionAsync(action.Action, sessionId, targetMobile.Id);
    }

    private ValueTask PublishVendorActionAsync(ContextMenuActionType action, long sessionId, Serial vendorSerial)
        => action switch
        {
            ContextMenuActionType.VendorBuy => _gameEventBusService.PublishAsync(
                new VendorBuyRequestedEvent(sessionId, vendorSerial)
            ),
            ContextMenuActionType.VendorSell => _gameEventBusService.PublishAsync(
                new VendorSellRequestedEvent(sessionId, vendorSerial)
            ),
            _ => ValueTask.CompletedTask
        };

    private async Task<UOMobileEntity?> ResolveTargetMobileAsync(
        Data.Session.GameSession session,
        Serial targetSerial,
        CancellationToken cancellationToken
    )
    {
        if (session.Character is not null && session.Character.Id == targetSerial)
        {
            return session.Character;
        }

        return await _mobileService.GetAsync(targetSerial, cancellationToken);
    }

    private static List<PopupContextMenuEntry> BuildEntries(UOMobileEntity targetMobile)
    {
        var entries = new List<PopupContextMenuEntry>
        {
            new(PaperdollEntryTag, 3006123)
        };

        if (targetMobile.TryGetCustomString(SellProfileIdKey, out var sellProfileId) &&
            !string.IsNullOrWhiteSpace(sellProfileId))
        {
            entries.Add(new(VendorBuyEntryTag, 3006103));
            entries.Add(new(VendorSellEntryTag, 3006104));
        }

        return entries;
    }

    private readonly record struct PendingContextMenuState(
        Serial TargetSerial,
        Dictionary<ushort, (ContextMenuActionType Action, string? ScriptKey)> EntryActions
    );
}
