using Moongate.Network.Packets.Outgoing.Combat;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Default melee combat orchestrator for player and NPC auto-attacks.
/// </summary>
public sealed class CombatService : ICombatService
{
    private const int MeleeRange = 1;
    private const int DefaultMinDamage = 1;
    private const int DefaultMaxDamage = 4;
    private const int DefaultAttackSpeed = 30;
    private static readonly TimeSpan AggressorTimeout = TimeSpan.FromMinutes(2);

    private readonly IMobileService _mobileService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly ITimerService _timerService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IDeathService _deathService;
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<Serial, int> _combatSequences = [];

    public CombatService(
        IMobileService mobileService,
        IGameNetworkSessionService gameNetworkSessionService,
        IOutgoingPacketQueue outgoingPacketQueue,
        ITimerService timerService,
        ISpatialWorldService spatialWorldService,
        IGameEventBusService gameEventBusService,
        IDeathService deathService
    )
    {
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _timerService = timerService;
        _spatialWorldService = spatialWorldService;
        _gameEventBusService = gameEventBusService;
        _deathService = deathService;
    }

    public async Task<bool> TrySetCombatantAsync(
        Serial attackerId,
        Serial defenderId,
        CancellationToken cancellationToken = default
    )
    {
        if (attackerId == Serial.Zero || defenderId == Serial.Zero || attackerId == defenderId)
        {
            return false;
        }

        var attacker = await ResolveMobileAsync(attackerId, cancellationToken);
        var defender = await ResolveMobileAsync(defenderId, cancellationToken);

        if (attacker is null || defender is null || !attacker.IsAlive || !defender.IsAlive || attacker.MapId != defender.MapId)
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        attacker.ExpireAggressors(nowUtc, AggressorTimeout);
        defender.ExpireAggressors(nowUtc, AggressorTimeout);
        attacker.CombatantId = defender.Id;
        attacker.Warmode = true;

        var delay = ResolveSwingDelay(attacker);
        attacker.NextCombatAtUtc = nowUtc.Add(delay);

        await PersistMobileAsync(attacker, cancellationToken);
        await PublishWarModeChangedAsync(attacker, cancellationToken);
        await _gameEventBusService.PublishAsync(
            new CombatStartedEvent(attacker.Id, defender.Id, attacker.MapId, attacker.Location, attacker),
            cancellationToken
        );
        SendChangeCombatant(attacker.Id, defender.Id);
        ScheduleSwing(attacker.Id, delay);

        return true;
    }

    public async Task<bool> ClearCombatantAsync(Serial attackerId, CancellationToken cancellationToken = default)
    {
        if (attackerId == Serial.Zero)
        {
            return false;
        }

        var attacker = await ResolveMobileAsync(attackerId, cancellationToken);

        if (attacker is null)
        {
            return false;
        }

        attacker.ClearCombatState();
        await PersistMobileAsync(attacker, cancellationToken);
        CancelSwing(attacker.Id);
        SendChangeCombatant(attacker.Id, Serial.Zero);

        return true;
    }

    private void CancelSwing(Serial attackerId)
    {
        _timerService.UnregisterTimersByName(GetTimerName(attackerId));

        lock (_syncRoot)
        {
            _combatSequences.Remove(attackerId);
        }
    }

    private static void ExpireAggressorEntries(UOMobileEntity mobile, DateTime nowUtc)
        => mobile.ExpireAggressors(nowUtc, AggressorTimeout);

    private static string GetTimerName(Serial attackerId)
        => $"combat:{(uint)attackerId}";

    private async Task<bool> IsCombatAllowedAsync(
        UOMobileEntity attacker,
        UOMobileEntity defender,
        CancellationToken cancellationToken
    )
    {
        var region = _spatialWorldService.ResolveRegion(attacker.MapId, attacker.Location);
        var isGuardedRegion = region is JsonGuardedRegion ||
                              region is JsonTownRegion townRegion && !townRegion.GuardsDisabled;
        var blockedReason = ResolveBlockedReason(attacker, defender);
        var allowed = blockedReason is null;

        await _gameEventBusService.PublishAsync(
            new CombatAttemptEvent(
                attacker.Id,
                defender.Id,
                attacker.MapId,
                attacker.Location,
                region?.Name,
                isGuardedRegion,
                allowed,
                blockedReason
            ),
            cancellationToken
        );

        return allowed;
    }

    private async Task PersistMobileAsync(UOMobileEntity mobile, CancellationToken cancellationToken)
    {
        await _mobileService.CreateOrUpdateAsync(mobile, cancellationToken);
        SyncRuntimeMobile(mobile);
    }

    private static bool ResolveHit(UOMobileEntity attacker, UOMobileEntity defender)
        => 50 + attacker.EffectiveHitChanceIncrease >= 50 + defender.EffectiveDefenseChanceIncrease;

    private static string? ResolveBlockedReason(UOMobileEntity attacker, UOMobileEntity defender)
    {
        var map = Map.GetMap(attacker.MapId);

        if (map is not null &&
            map.Rules.HasFlag(MapRules.HarmfulRestrictions) &&
            defender.Notoriety == Notoriety.Innocent)
        {
            return "map_harmful_restrictions";
        }

        return null;
    }

    private static int ResolveDamage(UOMobileEntity attacker)
    {
        var weapon = ResolveWeapon(attacker);
        var minDamage = weapon?.CombatStats?.DamageMin ?? attacker.MinWeaponDamage;
        var maxDamage = weapon?.CombatStats?.DamageMax ?? attacker.MaxWeaponDamage;

        if (minDamage <= 0)
        {
            minDamage = DefaultMinDamage;
        }

        if (maxDamage < minDamage)
        {
            maxDamage = minDamage;
        }

        if (maxDamage == 0)
        {
            maxDamage = DefaultMaxDamage;
        }

        var rolledDamage = Random.Shared.Next(minDamage, maxDamage + 1);

        return Math.Max(1, ScaleDamageAosLike(attacker, rolledDamage));
    }

    private static int ScaleDamageAosLike(UOMobileEntity attacker, int rolledDamage)
    {
        var strengthBonus = GetBonus(attacker.EffectiveStrength, 0.300, 100.0, 5.00);
        var anatomyBonus = GetBonus(GetSkillValue(attacker, UOSkillName.Anatomy), 0.500, 100.0, 5.00);
        var tacticsBonus = GetBonus(GetSkillValue(attacker, UOSkillName.Tactics), 0.625, 100.0, 6.25);
        var damageIncreaseBonus = Math.Clamp(Math.Max(0, attacker.EffectiveDamageIncrease), 0, 100) / 100.0;
        var totalBonus = strengthBonus + anatomyBonus + tacticsBonus + damageIncreaseBonus;

        return (int)(rolledDamage + rolledDamage * totalBonus);
    }

    private static double GetBonus(double value, double scalar, double threshold, double offset)
    {
        var bonus = value * scalar;

        if (value >= threshold)
        {
            bonus += offset;
        }

        return bonus / 100.0;
    }

    private static double GetSkillValue(UOMobileEntity mobile, UOSkillName skillName)
        => (mobile.GetSkill(skillName)?.Value ?? 0.0) / 10.0;

    private async Task<UOMobileEntity?> ResolveMobileAsync(Serial mobileId, CancellationToken cancellationToken)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        return await _mobileService.GetAsync(mobileId, cancellationToken);
    }

    private static UOItemEntity? ResolveWeapon(UOMobileEntity attacker)
        => attacker.GetEquippedItemsRuntime()
                   .FirstOrDefault(
                       item => item.EquippedLayer is ItemLayerType.OneHanded or
                               ItemLayerType.TwoHanded or
                               ItemLayerType.FirstValid
                   );

    private static TimeSpan ResolveSwingDelay(UOMobileEntity attacker)
    {
        var weapon = ResolveWeapon(attacker);
        var attackSpeed = weapon?.CombatStats?.AttackSpeed ?? DefaultAttackSpeed;
        var speedScale = 1.0 + Math.Max(0, attacker.EffectiveSwingSpeedIncrease) / 100.0;
        var seconds = Math.Clamp(attackSpeed / 20.0 / speedScale, 0.75, 3.0);

        return TimeSpan.FromSeconds(seconds);
    }

    private void ScheduleSwing(Serial attackerId, TimeSpan delay)
    {
        var timerName = GetTimerName(attackerId);
        _timerService.UnregisterTimersByName(timerName);

        int sequence;

        lock (_syncRoot)
        {
            _combatSequences.TryGetValue(attackerId, out var currentSequence);
            sequence = currentSequence + 1;
            _combatSequences[attackerId] = sequence;
        }

        _timerService.RegisterTimer(
            timerName,
            delay,
            () => ExecuteSwingAsync(attackerId, sequence).AsTask().GetAwaiter().GetResult(),
            delay
        );
    }

    private void SendChangeCombatant(Serial attackerId, Serial defenderId)
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(attackerId, out var session))
        {
            return;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, new ChangeCombatantPacket(defenderId));
    }

    private async ValueTask PublishWarModeChangedAsync(UOMobileEntity mobile, CancellationToken cancellationToken)
        => await _gameEventBusService.PublishAsync(new MobileWarModeChangedEvent(mobile), cancellationToken);

    private void SyncRuntimeMobile(UOMobileEntity updatedMobile)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(updatedMobile.Id, out var session))
        {
            session.Character = updatedMobile;
            session.CharacterId = updatedMobile.Id;
            session.IsMounted = updatedMobile.IsMounted;
        }
    }

    private async ValueTask ExecuteSwingAsync(Serial attackerId, int expectedSequence)
    {
        lock (_syncRoot)
        {
            if (!_combatSequences.TryGetValue(attackerId, out var currentSequence) || currentSequence != expectedSequence)
            {
                return;
            }
        }

        var attacker = await ResolveMobileAsync(attackerId, CancellationToken.None);

        if (attacker is null || attacker.CombatantId == Serial.Zero || !attacker.IsAlive || !attacker.Warmode)
        {
            await ClearCombatantAsync(attackerId);
            return;
        }

        var defender = await ResolveMobileAsync(attacker.CombatantId, CancellationToken.None);

        if (defender is null || !defender.IsAlive || attacker.MapId != defender.MapId)
        {
            await ClearCombatantAsync(attackerId);
            return;
        }

        var delay = ResolveSwingDelay(attacker);

        if (!attacker.Location.InRange(defender.Location, MeleeRange))
        {
            attacker.NextCombatAtUtc = DateTime.UtcNow.Add(delay);
            await PersistMobileAsync(attacker, CancellationToken.None);
            ScheduleSwing(attacker.Id, delay);
            return;
        }

        await _gameEventBusService.PublishAsync(
            new AggressiveActionEvent(
                attacker.Id,
                defender.Id,
                attacker.MapId,
                attacker.Location,
                attacker,
                defender
            )
        );

        if (!await IsCombatAllowedAsync(attacker, defender, CancellationToken.None))
        {
            await ClearCombatantAsync(attacker.Id);
            return;
        }

        await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
            new FightOccurringPacket(attacker.Id, defender.Id),
            attacker.MapId,
            attacker.Location
        );

        if (AnimationUtils.TryResolveAnimation(
                AnimationIntent.SwingPrimary,
                attacker.Body.Type,
                attacker.IsMounted,
                out var animation
            ))
        {
            await _gameEventBusService.PublishAsync(
                new MobilePlayAnimationEvent(
                    attacker.Id,
                    attacker.MapId,
                    attacker.Location,
                    animation.Action,
                    animation.FrameCount,
                    animation.RepeatCount,
                    animation.Forward,
                    animation.Repeat,
                    animation.Delay
                )
            );
        }

        var nowUtc = DateTime.UtcNow;
        ExpireAggressorEntries(attacker, nowUtc);
        ExpireAggressorEntries(defender, nowUtc);
        RefreshAggression(attacker.Aggressed, attacker.Id, defender.Id, nowUtc);
        RefreshAggression(defender.Aggressors, attacker.Id, defender.Id, nowUtc);
        attacker.LastCombatAtUtc = nowUtc;
        defender.LastCombatAtUtc = nowUtc;

        if (ResolveHit(attacker, defender))
        {
            var damage = ResolveDamage(attacker);
            defender.Hits = Math.Max(0, defender.Hits - damage);
            defender.IsAlive = defender.Hits > 0;

            await _gameEventBusService.PublishAsync(
                new CombatHitEvent(
                    attacker.Id,
                    defender.Id,
                    attacker.MapId,
                    attacker.Location,
                    damage,
                    attacker,
                    defender
                )
            );
        }
        else
        {
            await _gameEventBusService.PublishAsync(
                new CombatMissEvent(
                    attacker.Id,
                    defender.Id,
                    attacker.MapId,
                    attacker.Location,
                    attacker,
                    defender
                )
            );
        }

        attacker.NextCombatAtUtc = nowUtc.Add(delay);

        await PersistMobileAsync(attacker, CancellationToken.None);
        await PersistMobileAsync(defender, CancellationToken.None);

        if (_gameNetworkSessionService.TryGetByCharacterId(defender.Id, out var defenderSession))
        {
            _outgoingPacketQueue.Enqueue(defenderSession.SessionId, new PlayerStatusPacket(defender, 1));
        }

        if (!defender.IsAlive)
        {
            await _deathService.HandleDeathAsync(defender, attacker, CancellationToken.None);
            await ClearCombatantAsync(attacker.Id);
            return;
        }

        ScheduleSwing(attacker.Id, delay);
    }

    private static void RefreshAggression(
        List<AggressorInfo> entries,
        Serial attackerId,
        Serial defenderId,
        DateTime nowUtc,
        bool isCriminal = false,
        bool canReportMurder = false
    )
    {
        var updatedEntry = new AggressorInfo(attackerId, defenderId, nowUtc, isCriminal, canReportMurder);
        var index = entries.FindIndex(
            entry => entry.AttackerId == attackerId &&
                     entry.DefenderId == defenderId
        );

        if (index >= 0)
        {
            entries[index] = updatedEntry;
            return;
        }

        entries.Add(updatedEntry);
    }
}
