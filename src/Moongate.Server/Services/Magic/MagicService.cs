using Moongate.Network.Packets.Types.Targeting;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Internal.Cursors;
using Moongate.Server.Data.Magic;
using Moongate.Server.Interfaces.Characters;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Magic;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Serilog;

namespace Moongate.Server.Services.Magic;

/// <summary>
/// Coordinates cast preconditions, active cast state, and cast-delay timers.
/// </summary>
public sealed class MagicService : IMagicService
{
    private const string CastTimerNamePrefix = "spell_cast_";
    private const string SequenceTimerNamePrefix = "spell_sequence_";
    private static readonly TimeSpan SequenceDelay = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger = Log.ForContext<MagicService>();
    private readonly ITimerService _timerService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly ICharacterService _characterService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IPlayerTargetService _playerTargetService;
    private readonly IItemService _itemService;
    private readonly ISpellbookService _spellbookService;
    private readonly IMobileService _mobileService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IDeathService _deathService;
    private readonly ISpeechService _speechService;
    private readonly SpellRegistry _spellRegistry;
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<Serial, SpellCastContext> _activeCasts = [];

    public MagicService(
        ITimerService timerService,
        IGameEventBusService gameEventBusService,
        ICharacterService characterService,
        IGameNetworkSessionService gameNetworkSessionService,
        ISpatialWorldService spatialWorldService,
        IPlayerTargetService playerTargetService,
        IItemService itemService,
        ISpellbookService spellbookService,
        IMobileService mobileService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IDeathService deathService,
        ISpeechService speechService,
        SpellRegistry spellRegistry
    )
    {
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(gameEventBusService);
        ArgumentNullException.ThrowIfNull(characterService);
        ArgumentNullException.ThrowIfNull(gameNetworkSessionService);
        ArgumentNullException.ThrowIfNull(spatialWorldService);
        ArgumentNullException.ThrowIfNull(playerTargetService);
        ArgumentNullException.ThrowIfNull(itemService);
        ArgumentNullException.ThrowIfNull(spellbookService);
        ArgumentNullException.ThrowIfNull(mobileService);
        ArgumentNullException.ThrowIfNull(outgoingPacketQueue);
        ArgumentNullException.ThrowIfNull(deathService);
        ArgumentNullException.ThrowIfNull(speechService);
        ArgumentNullException.ThrowIfNull(spellRegistry);

        _timerService = timerService;
        _gameEventBusService = gameEventBusService;
        _characterService = characterService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _spatialWorldService = spatialWorldService;
        _playerTargetService = playerTargetService;
        _itemService = itemService;
        _spellbookService = spellbookService;
        _mobileService = mobileService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _deathService = deathService;
        _speechService = speechService;
        _spellRegistry = spellRegistry;
    }

    public bool IsCasting(Serial casterId)
    {
        lock (_syncRoot)
        {
            return _activeCasts.ContainsKey(casterId);
        }
    }

    public async ValueTask<bool> TryCastAsync(
        UOMobileEntity caster,
        int spellId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(caster);
        cancellationToken.ThrowIfCancellationRequested();

        if (!caster.IsAlive)
        {
            _logger.Debug("Rejecting spell cast for dead caster {CasterId}.", caster.Id);

            return false;
        }

        if (IsCasting(caster.Id))
        {
            _logger.Debug("Rejecting duplicate spell cast for caster {CasterId}.", caster.Id);

            return false;
        }

        var spell = _spellRegistry.Get(spellId);

        if (spell is null)
        {
            _logger.Debug("Rejecting spell cast for caster {CasterId} because spell {SpellId} is not registered.", caster.Id, spellId);

            return false;
        }

        if (caster.Mana < spell.ManaCost)
        {
            _logger.Debug(
                "Rejecting spell cast for caster {CasterId} because mana {Mana} is below cost {ManaCost}.",
                caster.Id,
                caster.Mana,
                spell.ManaCost
            );

            return false;
        }

        if (!await _spellbookService.MobileHasSpellAsync(caster, spell.SpellbookType, spell.SpellId, cancellationToken))
        {
            _logger.Debug(
                "Rejecting spell cast for caster {CasterId} because spell {SpellId} is not available in a spellbook.",
                caster.Id,
                spell.SpellId
            );

            return false;
        }

        if (!await HasReagentsAsync(caster, spell, cancellationToken))
        {
            _logger.Debug(
                "Rejecting spell cast for caster {CasterId} because spell {SpellId} is missing reagents.",
                caster.Id,
                spellId
            );

            return false;
        }

        if (!TryStartCast(caster.Id, spell))
        {
            _logger.Debug("Rejecting duplicate spell cast for caster {CasterId}.", caster.Id);

            return false;
        }

        caster.Mana = Math.Max(0, caster.Mana - spell.ManaCost);
        await SynchronizeMobileAsync(caster, cancellationToken);
        await _speechService.SpeakAsMobileAsync(caster, spell.Info.Mantra, cancellationToken: cancellationToken);

        return true;
    }

    public void Interrupt(Serial casterId)
    {
        SpellCastContext? activeCast;

        lock (_syncRoot)
        {
            if (!_activeCasts.Remove(casterId, out activeCast))
            {
                return;
            }
        }

        _timerService.UnregisterTimer(activeCast.TimerId);
        _logger.Debug("Interrupted active spell cast for caster {CasterId}.", casterId);
    }

    public bool TrySetTarget(Serial casterId, int spellId, Serial targetId)
    {
        if (targetId == Serial.Zero)
        {
            return false;
        }

        return TrySetTargetAsync(casterId, spellId, SpellTargetData.Mobile(targetId))
               .AsTask()
               .GetAwaiter()
               .GetResult();
    }

    public async ValueTask<bool> TrySetTargetAsync(
        Serial casterId,
        int spellId,
        SpellTargetData target,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        SpellCastContext? activeCast;
        ISpell? spell = null;
        var completeImmediately = false;

        lock (_syncRoot)
        {
            if (!_activeCasts.TryGetValue(casterId, out activeCast) || activeCast.SpellId != spellId)
            {
                return false;
            }

            activeCast.Target = target;
            completeImmediately = activeCast.State == SpellStateType.Sequencing;

            if (completeImmediately)
            {
                spell = _spellRegistry.Get(activeCast.SpellId);
            }
        }

        if (!completeImmediately)
        {
            return true;
        }

        if (spell is null)
        {
            Interrupt(casterId);

            return false;
        }

        return await CompleteSequencedCastAsync(casterId, activeCast!, spell, cancellationToken);
    }

    public async ValueTask OnCastTimerExpiredAsync(Serial casterId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SpellCastContext? activeCast;

        lock (_syncRoot)
        {
            if (!_activeCasts.TryGetValue(casterId, out activeCast))
            {
                return;
            }
        }

        var spell = _spellRegistry.Get(activeCast.SpellId);

        if (spell is null)
        {
            _logger.Debug("Cannot complete cast timer for caster {CasterId}: spell {SpellId} is no longer registered.", casterId, activeCast.SpellId);

            return;
        }

        var caster = await ResolveMobileAsync(casterId, cancellationToken);

        if (caster is null)
        {
            RemoveActiveCast(casterId);
            _logger.Debug("Cannot complete cast timer for caster {CasterId}: no live mobile found.", casterId);

            return;
        }

        if (activeCast.State == SpellStateType.Sequencing)
        {
            RemoveActiveCast(casterId);
            _logger.Debug(
                "Sequencing expired for caster {CasterId} and spell {SpellId} without a target.",
                casterId,
                activeCast.SpellId
            );

            return;
        }

        if (activeCast.Target.Kind != SpellTargetKind.None)
        {
            RemoveActiveCast(casterId);
            await ApplyResolvedSpellEffectAsync(caster, spell, activeCast.Target, cancellationToken);

            return;
        }

        if (spell.Targeting == SpellTargetingType.None)
        {
            RemoveActiveCast(casterId);
            await ApplyResolvedSpellEffectAsync(caster, spell, SpellTargetData.None(), cancellationToken);

            return;
        }

        if (!_gameNetworkSessionService.TryGetByCharacterId(casterId, out var session))
        {
            RemoveActiveCast(casterId);

            if (spell.Targeting == SpellTargetingType.OptionalMobile)
            {
                await ApplyResolvedSpellEffectAsync(caster, spell, SpellTargetData.None(), cancellationToken);
            }

            return;
        }

        await BeginSequencingAsync(activeCast, spell, session.SessionId);
    }

    private async ValueTask ApplyResolvedSpellEffectAsync(
        UOMobileEntity caster,
        ISpell spell,
        SpellTargetData target,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targetMobile = target.Kind == SpellTargetKind.Mobile
            ? await ResolveMobileAsync(target.TargetId, cancellationToken)
            : null;
        var targetItem = target.Kind == SpellTargetKind.Item
            ? await ResolveItemAsync(target.TargetId, cancellationToken)
            : null;

        if (spell.Targeting == SpellTargetingType.OptionalMobile && target.Kind == SpellTargetKind.None)
        {
            targetMobile = caster;
        }

        if ((spell.Targeting == SpellTargetingType.RequiredMobile && targetMobile is null) ||
            (spell.Targeting == SpellTargetingType.RequiredItem && targetItem is null) ||
            (spell.Targeting == SpellTargetingType.RequiredLocation && target.Kind != SpellTargetKind.Location))
        {
            _logger.Debug(
                "Skipping spell effect for caster {CasterId} and spell {SpellId}: required target was unavailable.",
                caster.Id,
                spell.SpellId
            );

            return;
        }

        if (!await TryConsumeReagentsAsync(caster, spell, cancellationToken))
        {
            _logger.Debug(
                "Skipping spell effect for caster {CasterId} and spell {SpellId}: reagents were unavailable at completion.",
                caster.Id,
                spell.SpellId
            );

            return;
        }

        var context = new SpellExecutionContext(
            caster,
            target,
            targetMobile,
            targetItem,
            _spatialWorldService,
            _gameEventBusService,
            _timerService,
            _itemService
        );

        await spell.ApplyEffectAsync(context, cancellationToken);
        await SynchronizeAffectedMobilesAsync(caster, targetMobile, cancellationToken);
        _logger.Debug("Completed cast for caster {CasterId} and spell {SpellId}.", caster.Id, spell.SpellId);
    }

    private async ValueTask SynchronizeAffectedMobilesAsync(
        UOMobileEntity caster,
        UOMobileEntity? targetMobile,
        CancellationToken cancellationToken
    )
    {
        Dictionary<Serial, UOMobileEntity> affectedMobiles = [];
        TrackAffectedMobile(affectedMobiles, caster);
        TrackAffectedMobile(affectedMobiles, targetMobile);

        foreach (var mobile in affectedMobiles.Values)
        {
            NormalizeAliveState(mobile);
            await SynchronizeMobileAsync(mobile, cancellationToken);
        }

        foreach (var mobile in affectedMobiles.Values)
        {
            if (!mobile.IsAlive)
            {
                await _deathService.HandleDeathAsync(mobile, mobile.Id == caster.Id ? null : caster, cancellationToken);
            }
        }
    }

    private async ValueTask SynchronizeMobileAsync(UOMobileEntity mobile, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(mobile);

        _spatialWorldService.AddOrUpdateMobile(mobile);
        await _mobileService.CreateOrUpdateAsync(mobile, cancellationToken);

        if (_gameNetworkSessionService.TryGetByCharacterId(mobile.Id, out var session))
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, new PlayerStatusPacket(mobile, 1));
        }
    }

    private static void NormalizeAliveState(UOMobileEntity mobile)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.Hits <= 0)
        {
            mobile.Hits = 0;
            mobile.IsAlive = false;
        }
    }

    private static void TrackAffectedMobile(
        Dictionary<Serial, UOMobileEntity> affectedMobiles,
        UOMobileEntity? mobile
    )
    {
        ArgumentNullException.ThrowIfNull(affectedMobiles);

        if (mobile is null || mobile.Id == Serial.Zero)
        {
            return;
        }

        affectedMobiles[mobile.Id] = mobile;
    }

    private async ValueTask BeginSequencingAsync(SpellCastContext activeCast, ISpell spell, long sessionId)
    {
        var timerName = GetSequenceTimerName(activeCast.CasterId, activeCast.SpellId);

        lock (_syncRoot)
        {
            if (!_activeCasts.TryGetValue(activeCast.CasterId, out var current) || current.SpellId != activeCast.SpellId)
            {
                return;
            }

            current.State = SpellStateType.Sequencing;
        }

        var timerId = _timerService.RegisterTimer(
            timerName,
            SequenceDelay,
            () => OnCastTimerExpiredAsync(activeCast.CasterId).AsTask().GetAwaiter().GetResult()
        );

        lock (_syncRoot)
        {
            if (!_activeCasts.TryGetValue(activeCast.CasterId, out var current) || current.SpellId != activeCast.SpellId)
            {
                _timerService.UnregisterTimer(timerId);

                return;
            }

            current.TimerId = timerId;
        }

        var selectionType = spell.Targeting == SpellTargetingType.RequiredLocation
            ? TargetCursorSelectionType.SelectLocation
            : TargetCursorSelectionType.SelectObject;

        var cursorId = await _playerTargetService.SendTargetCursorAsync(
            sessionId,
            callback => HandleSequencingTargetCallback(activeCast.CasterId, activeCast.SpellId, spell.Targeting, sessionId, callback),
            selectionType,
            TargetCursorType.Neutral
        );

        if (cursorId == Serial.Zero)
        {
            RemoveActiveCast(activeCast.CasterId, timerId);

            return;
        }

        _logger.Debug(
            "Entered sequencing for caster {CasterId}, spell {SpellId}, timeout {SequenceDelay}.",
            activeCast.CasterId,
            activeCast.SpellId,
            SequenceDelay
        );
    }

    private async ValueTask<bool> CompleteSequencedCastAsync(
        Serial casterId,
        SpellCastContext activeCast,
        ISpell spell,
        CancellationToken cancellationToken
    )
    {
        RemoveActiveCast(casterId, activeCast.TimerId);

        var caster = await ResolveMobileAsync(casterId, cancellationToken);

        if (caster is null)
        {
            return false;
        }

        await ApplyResolvedSpellEffectAsync(caster, spell, activeCast.Target, cancellationToken);

        return true;
    }

    private void HandleSequencingTargetCallback(
        Serial casterId,
        int spellId,
        SpellTargetingType targeting,
        long sessionId,
        PendingCursorCallback callback
    )
    {
        if (callback.Packet.CursorType == TargetCursorType.CancelCurrentTargeting)
        {
            Interrupt(casterId);

            return;
        }

        if (!TryCreateTargetData(targeting, sessionId, callback, out var target))
        {
            Interrupt(casterId);

            return;
        }

        TrySetTargetAsync(casterId, spellId, target).AsTask().GetAwaiter().GetResult();
    }

    private void RemoveActiveCast(Serial casterId, string? timerId = null)
    {
        SpellCastContext? activeCast;

        lock (_syncRoot)
        {
            if (!_activeCasts.Remove(casterId, out activeCast))
            {
                return;
            }
        }

        var effectiveTimerId = string.IsNullOrWhiteSpace(timerId) ? activeCast?.TimerId : timerId;

        if (!string.IsNullOrWhiteSpace(effectiveTimerId))
        {
            _timerService.UnregisterTimer(effectiveTimerId);
        }
    }

    private async ValueTask<bool> HasReagentsAsync(
        UOMobileEntity caster,
        ISpell spell,
        CancellationToken cancellationToken
    )
    {
        var requiredReagents = BuildRequiredReagentCounts(spell.Info);

        if (requiredReagents.Count == 0)
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var backpack = await _characterService.GetBackpackWithItemsAsync(caster);
        cancellationToken.ThrowIfCancellationRequested();

        if (backpack is null)
        {
            return false;
        }

        var availableReagents = new Dictionary<string, int>(StringComparer.Ordinal);
        CountAvailableReagents(backpack, availableReagents, cancellationToken);

        return HasRequiredReagents(requiredReagents, availableReagents);
    }

    private async ValueTask<bool> TryConsumeReagentsAsync(
        UOMobileEntity caster,
        ISpell spell,
        CancellationToken cancellationToken
    )
    {
        var requiredReagents = BuildRequiredReagentCounts(spell.Info);

        if (requiredReagents.Count == 0)
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var backpack = await _characterService.GetBackpackWithItemsAsync(caster);
        cancellationToken.ThrowIfCancellationRequested();

        if (backpack is null)
        {
            return false;
        }

        var availableReagents = new Dictionary<string, int>(StringComparer.Ordinal);
        CountAvailableReagents(backpack, availableReagents, cancellationToken);

        if (!HasRequiredReagents(requiredReagents, availableReagents))
        {
            return false;
        }

        var remainingReagents = new Dictionary<string, int>(requiredReagents, StringComparer.Ordinal);
        var changedStacks = new Dictionary<Serial, UOItemEntity>();
        var deletedStacks = new List<UOItemEntity>();
        ConsumeReagentsRecursive(backpack, remainingReagents, changedStacks, deletedStacks, cancellationToken);

        if (remainingReagents.Values.Any(static amount => amount > 0))
        {
            return false;
        }

        for (var index = 0; index < deletedStacks.Count; index++)
        {
            _ = await _itemService.DeleteItemAsync(deletedStacks[index].Id);
        }

        foreach (var changedStack in changedStacks.Values)
        {
            await _itemService.UpsertItemAsync(changedStack);
        }

        return true;
    }

    private static Dictionary<string, int> BuildRequiredReagentCounts(SpellInfo spellInfo)
    {
        var requiredReagents = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < spellInfo.Reagents.Length; index++)
        {
            var reagent = spellInfo.Reagents[index];
            var amount = spellInfo.ReagentAmounts[index];
            var templateId = ReagentCatalog.GetTemplateId(reagent);

            if (string.IsNullOrWhiteSpace(templateId) || amount <= 0)
            {
                continue;
            }

            requiredReagents[templateId] = requiredReagents.GetValueOrDefault(templateId) + amount;
        }

        return requiredReagents;
    }

    private static void CountAvailableReagents(
        UOItemEntity container,
        Dictionary<string, int> availableReagents,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (container.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) &&
            !string.IsNullOrWhiteSpace(templateId))
        {
            availableReagents[templateId] = availableReagents.GetValueOrDefault(templateId) + Math.Max(1, container.Amount);
        }

        for (var index = 0; index < container.Items.Count; index++)
        {
            CountAvailableReagents(container.Items[index], availableReagents, cancellationToken);
        }
    }

    private static void ConsumeReagentsRecursive(
        UOItemEntity container,
        Dictionary<string, int> remainingReagents,
        Dictionary<Serial, UOItemEntity> changedStacks,
        List<UOItemEntity> deletedStacks,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        for (var index = container.Items.Count - 1; index >= 0 && HasOutstandingReagents(remainingReagents); index--)
        {
            var child = container.Items[index];

            if (TryGetTemplateId(child, out var templateId) &&
                remainingReagents.TryGetValue(templateId, out var remainingAmount) &&
                remainingAmount > 0)
            {
                var availableAmount = Math.Max(1, child.Amount);
                var consumeAmount = Math.Min(availableAmount, remainingAmount);
                child.Amount = Math.Max(0, child.Amount - consumeAmount);
                remainingReagents[templateId] = remainingAmount - consumeAmount;

                if (child.Amount <= 0)
                {
                    container.RemoveItem(child.Id);
                    deletedStacks.Add(child);

                    continue;
                }

                changedStacks[child.Id] = child;
            }

            if (child.Items.Count > 0)
            {
                ConsumeReagentsRecursive(child, remainingReagents, changedStacks, deletedStacks, cancellationToken);
            }
        }
    }

    private static bool HasOutstandingReagents(Dictionary<string, int> remainingReagents)
        => remainingReagents.Values.Any(static amount => amount > 0);

    private static bool HasRequiredReagents(
        Dictionary<string, int> requiredReagents,
        Dictionary<string, int> availableReagents
    )
    {
        foreach (var requiredReagent in requiredReagents)
        {
            if (!availableReagents.TryGetValue(requiredReagent.Key, out var availableAmount) ||
                availableAmount < requiredReagent.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetTemplateId(UOItemEntity item, out string templateId)
    {
        templateId = string.Empty;

        if (!item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var rawTemplateId) ||
            string.IsNullOrWhiteSpace(rawTemplateId))
        {
            return false;
        }

        templateId = rawTemplateId;

        return true;
    }

    private bool TryStartCast(Serial casterId, ISpell spell)
    {
        var timerId = GetTimerName(casterId, spell.SpellId);
        var context = new SpellCastContext(casterId, spell.SpellId, SpellStateType.Casting, timerId);

        lock (_syncRoot)
        {
            if (_activeCasts.ContainsKey(casterId))
            {
                return false;
            }

            _activeCasts[casterId] = context;
        }

        var registeredTimerId = _timerService.RegisterTimer(
            timerId,
            spell.CastDelay,
            () => OnCastTimerExpiredAsync(casterId).AsTask().GetAwaiter().GetResult()
        );

        lock (_syncRoot)
        {
            if (_activeCasts.TryGetValue(casterId, out var activeCast) && activeCast.SpellId == spell.SpellId)
            {
                activeCast.TimerId = registeredTimerId;
            }
        }

        _logger.Debug(
            "Started spell cast for caster {CasterId}, spell {SpellId}, delay {CastDelay}.",
            casterId,
            spell.SpellId,
            spell.CastDelay
        );

        return true;
    }

    private static string GetTimerName(Serial casterId, int spellId)
        => $"{CastTimerNamePrefix}{casterId}_{spellId}";

    private static string GetSequenceTimerName(Serial casterId, int spellId)
        => $"{SequenceTimerNamePrefix}{casterId}_{spellId}";

    private bool TryCreateTargetData(
        SpellTargetingType targeting,
        long sessionId,
        PendingCursorCallback callback,
        out SpellTargetData target
    )
    {
        target = SpellTargetData.None();

        return targeting switch
        {
            SpellTargetingType.OptionalMobile or SpellTargetingType.RequiredMobile
                when callback.Packet.ClickedOnId != Serial.Zero
                => TryCreateMobileTarget(callback, out target),
            SpellTargetingType.RequiredItem
                when callback.Packet.ClickedOnId != Serial.Zero
                => TryCreateItemTarget(callback, out target),
            SpellTargetingType.RequiredLocation
                => TryCreateLocationTarget(sessionId, callback, out target),
            _ => false
        };
    }

    private static bool TryCreateMobileTarget(PendingCursorCallback callback, out SpellTargetData target)
    {
        target = SpellTargetData.Mobile(callback.Packet.ClickedOnId);

        return true;
    }

    private static bool TryCreateItemTarget(PendingCursorCallback callback, out SpellTargetData target)
    {
        target = SpellTargetData.Item(callback.Packet.ClickedOnId, callback.Packet.Location, callback.Packet.Graphic);

        return true;
    }

    private bool TryCreateLocationTarget(long sessionId, PendingCursorCallback callback, out SpellTargetData target)
    {
        if (!_gameNetworkSessionService.TryGet(sessionId, out var session) || session.Character is null)
        {
            target = SpellTargetData.None();

            return false;
        }

        target = SpellTargetData.FromLocation(session.Character.MapId, callback.Packet.Location, callback.Packet.Graphic);

        return true;
    }

    private async ValueTask<UOMobileEntity?> ResolveMobileAsync(Serial mobileId, CancellationToken cancellationToken)
    {
        if (mobileId == Serial.Zero)
        {
            return null;
        }

        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        var activeMobiles = _spatialWorldService.GetActiveSectors()
                                                .SelectMany(sector => sector.GetMobiles())
                                                .FirstOrDefault(mobile => mobile.Id == mobileId);

        if (activeMobiles is not null)
        {
            return activeMobiles;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _characterService.GetCharacterAsync(mobileId);
    }

    private async ValueTask<UOItemEntity?> ResolveItemAsync(Serial itemId, CancellationToken cancellationToken)
    {
        if (itemId == Serial.Zero)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _itemService.GetItemAsync(itemId);
    }
}
