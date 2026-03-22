using Moongate.Network.Packets.Outgoing.Combat;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Combat;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
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
    private readonly IItemService _itemService;
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
        IItemService itemService,
        IDeathService deathService
    )
    {
        _mobileService = mobileService;
        _gameNetworkSessionService = gameNetworkSessionService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _timerService = timerService;
        _spatialWorldService = spatialWorldService;
        _gameEventBusService = gameEventBusService;
        _itemService = itemService;
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

        if (attacker.CombatantId == defender.Id && attacker.Warmode && HasScheduledSwing(attacker.Id))
        {
            return true;
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

    private bool HasScheduledSwing(Serial attackerId)
    {
        lock (_syncRoot)
        {
            return _combatSequences.ContainsKey(attackerId);
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
        => ResolveHitScore(attacker, defender) >= 0.5;

    private static double ResolveHitScore(UOMobileEntity attacker, UOMobileEntity defender)
    {
        var attackSkillValue = GetSkillValue(attacker, ResolveAttackSkill(attacker));
        var defenseSkillValue = GetSkillValue(defender, ResolveDefenseSkill(defender));
        var modifierDelta = (attacker.EffectiveHitChanceIncrease - defender.EffectiveDefenseChanceIncrease) / 100.0;

        return Math.Clamp(0.5 + ((attackSkillValue - defenseSkillValue) / 200.0) + modifierDelta, 0.0, 1.0);
    }

    private static string? ResolveBlockedReason(UOMobileEntity attacker, UOMobileEntity defender)
    {
        var map = Map.GetMap(attacker.MapId);

        if (map is not null &&
            map.Rules.HasFlag(MapRules.HarmfulRestrictions) &&
            defender.IsPlayer &&
            defender.Notoriety == Notoriety.Innocent)
        {
            return "map_harmful_restrictions";
        }

        return null;
    }

    private static int ResolveDamage(UOMobileEntity attacker)
        => ResolveDamage(attacker, default);

    private static int ResolveDamage(UOMobileEntity attacker, AttackProfile attackProfile)
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

        return Math.Max(1, ScaleDamageAosLike(attacker, rolledDamage, attackProfile.RangedDamageIncrease));
    }

    private static int ScaleDamageAosLike(UOMobileEntity attacker, int rolledDamage, int rangedDamageIncreaseBonus = 0)
    {
        var strengthBonus = GetBonus(attacker.EffectiveStrength, 0.300, 100.0, 5.00);
        var anatomyBonus = GetBonus(GetSkillValue(attacker, UOSkillName.Anatomy), 0.500, 100.0, 5.00);
        var tacticsBonus = GetBonus(GetSkillValue(attacker, UOSkillName.Tactics), 0.625, 100.0, 6.25);
        var totalDamageIncrease = Math.Clamp(Math.Max(0, attacker.EffectiveDamageIncrease + rangedDamageIncreaseBonus), 0, 100);
        var damageIncreaseBonus = totalDamageIncrease / 100.0;
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

    private static UOSkillName ResolveAttackSkill(UOMobileEntity attacker)
        => ResolveWeapon(attacker)?.WeaponSkill ?? UOSkillName.Wrestling;

    private static UOSkillName ResolveDefenseSkill(UOMobileEntity defender)
        => ResolveWeapon(defender)?.WeaponSkill ?? UOSkillName.Wrestling;

    private async Task<UOMobileEntity?> ResolveMobileAsync(Serial mobileId, CancellationToken cancellationToken)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session) && session.Character is not null)
        {
            return session.Character;
        }

        var persistedMobile = await _mobileService.GetAsync(mobileId, cancellationToken);
        var liveMobile = TryResolveLiveMobile(mobileId, persistedMobile);

        return liveMobile ?? persistedMobile;
    }

    private UOMobileEntity? TryResolveLiveMobile(Serial mobileId, UOMobileEntity? persistedMobile)
    {
        if (persistedMobile is not null)
        {
            var sector = _spatialWorldService.GetSectorByLocation(persistedMobile.MapId, persistedMobile.Location);
            var sectorMobile = sector?.GetEntity<UOMobileEntity>(mobileId);

            if (sectorMobile is not null)
            {
                return sectorMobile;
            }

            var nearbyMobile = _spatialWorldService.GetNearbyMobiles(
                persistedMobile.Location,
                MapSectorConsts.MaxViewRange,
                persistedMobile.MapId
            ).FirstOrDefault(mobile => mobile.Id == mobileId);

            if (nearbyMobile is not null)
            {
                return nearbyMobile;
            }
        }

        foreach (var sector in _spatialWorldService.GetActiveSectors())
        {
            var sectorMobile = sector.GetEntity<UOMobileEntity>(mobileId);

            if (sectorMobile is not null)
            {
                return sectorMobile;
            }
        }

        return null;
    }

    private static UOItemEntity? ResolveWeapon(UOMobileEntity attacker)
        => attacker.GetEquippedItemsRuntime()
                   .FirstOrDefault(
                       item => item.EquippedLayer is ItemLayerType.OneHanded or
                               ItemLayerType.TwoHanded or
                               ItemLayerType.FirstValid
                   );

    private static UOItemEntity? ResolveQuiver(UOMobileEntity attacker)
        => attacker.GetEquippedItemsRuntime()
                   .FirstOrDefault(item => item.EquippedLayer == ItemLayerType.Cloak && item.IsQuiver);

    private static AttackProfile ResolveAttackProfile(UOMobileEntity attacker)
    {
        var weapon = ResolveWeapon(attacker);
        var quiver = ResolveQuiver(attacker);
        var maxRange = weapon?.CombatStats?.RangeMax ?? 0;

        if (maxRange <= 0)
        {
            maxRange = MeleeRange;
        }

        return new(
            weapon,
            quiver,
            maxRange,
            weapon?.AmmoItemId,
            weapon?.AmmoEffectId,
            quiver?.QuiverLowerAmmoCost ?? 0,
            quiver?.QuiverDamageIncrease ?? 0
        );
    }

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

    private async ValueTask FaceTowardDefenderAsync(UOMobileEntity attacker, UOMobileEntity defender)
    {
        var desiredDirection = Point3D.GetBaseDirection(attacker.Location.GetDirectionTo(defender.Location));
        var currentDirection = Point3D.GetBaseDirection(attacker.Direction);

        if (desiredDirection == currentDirection)
        {
            return;
        }

        attacker.Direction = desiredDirection;
        SyncRuntimeMobile(attacker);

        await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
            new MobileMovingPacket(attacker),
            attacker.MapId,
            attacker.Location
        );
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

        await FaceTowardDefenderAsync(attacker, defender);

        var delay = ResolveSwingDelay(attacker);
        var attackProfile = ResolveAttackProfile(attacker);

        if (!attacker.Location.InRange(defender.Location, attackProfile.MaxRange))
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

        if (attackProfile.IsRanged &&
            !await TryConsumeAmmoAsync(attacker, attackProfile, CancellationToken.None))
        {
            await ClearCombatantAsync(attacker.Id, CancellationToken.None);
            return;
        }

        await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
            new FightOccurringPacket(attacker.Id, defender.Id),
            attacker.MapId,
            attacker.Location
        );

        if (attackProfile.IsRanged && attackProfile.ProjectileEffectId is not null)
        {
            await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
                EffectsFactory.CreateMoving(
                    (ushort)attackProfile.ProjectileEffectId.Value,
                    attacker.Id,
                    defender.Id,
                    attacker.Location,
                    defender.Location
                ),
                attacker.MapId,
                attacker.Location
            );
        }

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
            var damage = ResolveDamage(attacker, attackProfile);
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

    private async Task<bool> TryConsumeAmmoAsync(
        UOMobileEntity attacker,
        AttackProfile attackProfile,
        CancellationToken cancellationToken
    )
    {
        var ammoItemId = attackProfile.AmmoItemId!.Value;
        var quiver = attackProfile.Quiver;
        var backpackId = ResolveBackpackId(attacker);

        var backpack = backpackId == Serial.Zero
            ? null
            : await _itemService.GetItemAsync(backpackId);

        if (!ContainsAmmo(quiver, ammoItemId) && !ContainsAmmo(backpack, ammoItemId))
        {
            return !attacker.IsPlayer;
        }

        if (attackProfile.LowerAmmoCost > 0 &&
            Random.Shared.Next(100) < attackProfile.LowerAmmoCost)
        {
            _ = cancellationToken;
            return true;
        }

        if (quiver is not null &&
            TryConsumeAmmoRecursive(quiver, ammoItemId, out var changedQuiverStack, out var deletedQuiverStack))
        {
            if (changedQuiverStack is not null)
            {
                await _itemService.UpsertItemAsync(changedQuiverStack);
            }

            if (deletedQuiverStack is not null)
            {
                _ = await _itemService.DeleteItemAsync(deletedQuiverStack.Id);
            }

            RefreshConsumedAmmoForSession(attacker.Id, quiver, deletedQuiverStack);
            _ = cancellationToken;
            return true;
        }

        if (backpack is null ||
            !TryConsumeAmmoRecursive(backpack, ammoItemId, out var changedStack, out var deletedStack))
        {
            return false;
        }

        if (changedStack is not null)
        {
            await _itemService.UpsertItemAsync(changedStack);
        }

        if (deletedStack is not null)
        {
            _ = await _itemService.DeleteItemAsync(deletedStack.Id);
        }

        RefreshConsumedAmmoForSession(attacker.Id, backpack, deletedStack);
        _ = cancellationToken;
        return true;
    }

    private void RefreshConsumedAmmoForSession(Serial attackerId, UOItemEntity backpack, UOItemEntity? deletedStack)
    {
        if (!_gameNetworkSessionService.TryGetByCharacterId(attackerId, out var session))
        {
            return;
        }

        _outgoingPacketQueue.Enqueue(session.SessionId, new AddMultipleItemsToContainerPacket(backpack));

        if (deletedStack is not null)
        {
            _outgoingPacketQueue.Enqueue(session.SessionId, new DeleteObjectPacket(deletedStack.Id));
        }
    }

    private static Serial ResolveBackpackId(UOMobileEntity attacker)
    {
        if (attacker.BackpackId != Serial.Zero)
        {
            return attacker.BackpackId;
        }

        return attacker.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var backpackId)
                   ? backpackId
                   : Serial.Zero;
    }

    private static bool ContainsAmmo(UOItemEntity? container, int ammoItemId)
    {
        if (container is null)
        {
            return false;
        }

        for (var index = container.Items.Count - 1; index >= 0; index--)
        {
            var child = container.Items[index];

            if (child.ItemId == ammoItemId)
            {
                return true;
            }

            if (ContainsAmmo(child, ammoItemId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumeAmmoRecursive(
        UOItemEntity container,
        int ammoItemId,
        out UOItemEntity? changedStack,
        out UOItemEntity? deletedStack
    )
    {
        changedStack = null;
        deletedStack = null;

        for (var index = container.Items.Count - 1; index >= 0; index--)
        {
            var child = container.Items[index];

            if (child.ItemId == ammoItemId)
            {
                child.Amount--;

                if (child.Amount <= 0)
                {
                    container.RemoveItem(child.Id);
                    deletedStack = child;
                }
                else
                {
                    changedStack = child;
                }

                return true;
            }

            if (TryConsumeAmmoRecursive(child, ammoItemId, out changedStack, out deletedStack))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct AttackProfile(
        UOItemEntity? Weapon,
        UOItemEntity? Quiver,
        int MaxRange,
        int? AmmoItemId,
        int? ProjectileEffectId,
        int LowerAmmoCost,
        int RangedDamageIncrease
    )
    {
        public bool IsRanged => Weapon is not null &&
                                AmmoItemId is not null &&
                                ProjectileEffectId is not null &&
                                MaxRange > MeleeRange;
    }
}
