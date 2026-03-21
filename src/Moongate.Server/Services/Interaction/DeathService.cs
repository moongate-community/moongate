using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.UO.Data.Constants;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Interaction;

/// <summary>
/// Resolves mobile death, corpse creation, loot transfer and corpse decay.
/// </summary>
public sealed class DeathService : IDeathService
{
    private static readonly ItemLayerType[] ExcludedCorpseLayers =
    [
        ItemLayerType.Backpack,
        ItemLayerType.Bank,
        ItemLayerType.Mount,
        ItemLayerType.Hair,
        ItemLayerType.FacialHair,
        ItemLayerType.ShopBuy,
        ItemLayerType.ShopResale,
        ItemLayerType.ShopSell
    ];

    private readonly IMobileService _mobileService;
    private readonly IItemService _itemService;
    private readonly ISpatialWorldService _spatialWorldService;
    private readonly ITimerService _timerService;
    private readonly IGameEventBusService _gameEventBusService;
    private readonly IFameKarmaService _fameKarmaService;
    private readonly MoongateConfig _config;
    private readonly ILuaBrainRunner? _luaBrainRunner;

    public DeathService(
        IMobileService mobileService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService,
        ITimerService timerService,
        IGameEventBusService gameEventBusService,
        IFameKarmaService fameKarmaService,
        MoongateConfig config,
        ILuaBrainRunner? luaBrainRunner = null
    )
    {
        _mobileService = mobileService;
        _itemService = itemService;
        _spatialWorldService = spatialWorldService;
        _timerService = timerService;
        _gameEventBusService = gameEventBusService;
        _fameKarmaService = fameKarmaService;
        _config = config;
        _luaBrainRunner = luaBrainRunner;
    }

    public async Task<bool> ForceDeathAsync(
        UOMobileEntity victim,
        UOMobileEntity? killer,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(victim);

        victim.Hits = 0;
        victim.IsAlive = false;

        return await HandleDeathAsync(victim, killer, cancellationToken);
    }

    public async Task<bool> HandleDeathAsync(
        UOMobileEntity victim,
        UOMobileEntity? killer,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(victim);

        if (victim.Id == Serial.Zero || victim.IsAlive)
        {
            return false;
        }

        var regionName = _spatialWorldService.ResolveRegion(victim.MapId, victim.Location)?.Name;
        var deathPayload = BuildDeathPayload(victim, killer, regionName, null);

        EnqueueLuaDeathHook(victim, LuaBrainDeathHookType.BeforeDeath, killer?.Id, deathPayload);
        await _gameEventBusService.PublishAsync(
            new MobileBeforeDeathEvent(victim, killer, regionName),
            cancellationToken
        );

        victim.ClearCombatState();
        victim.Warmode = false;
        victim.Aggressors.Clear();
        victim.Aggressed.Clear();

        UOItemEntity? corpse = null;

        if (victim.IsPlayer)
        {
            await _mobileService.CreateOrUpdateAsync(victim, cancellationToken);
        }
        else
        {
            corpse = await CreateCorpseAsync(victim, cancellationToken);
            await MoveLootToCorpseAsync(victim, corpse, cancellationToken);
            _spatialWorldService.AddOrUpdateItem(corpse, corpse.MapId);
            await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
                new MobileDeathAnimationPacket(victim.Id, corpse.Id),
                victim.MapId,
                victim.Location
            );
            _spatialWorldService.RemoveEntity(victim.Id);
            await _mobileService.DeleteAsync(victim.Id, cancellationToken);
            ScheduleCorpseDecay(corpse);
        }

        if (killer is not null && !victim.IsPlayer && killer.IsPlayer)
        {
            await _fameKarmaService.AwardNpcKillAsync(victim, killer, cancellationToken);
        }

        deathPayload = BuildDeathPayload(victim, killer, regionName, corpse);
        EnqueueLuaDeathHook(victim, LuaBrainDeathHookType.Death, killer?.Id, deathPayload);
        await _gameEventBusService.PublishAsync(
            new MobileDeathEvent(victim, killer, corpse, regionName),
            cancellationToken
        );

        EnqueueLuaDeathHook(victim, LuaBrainDeathHookType.AfterDeath, killer?.Id, deathPayload);
        await _gameEventBusService.PublishAsync(
            new MobileAfterDeathEvent(victim, killer, corpse, regionName),
            cancellationToken
        );

        return true;
    }

    private static Dictionary<string, object?> BuildDeathPayload(
        UOMobileEntity victim,
        UOMobileEntity? killer,
        string? regionName,
        UOItemEntity? corpse
    )
    {
        var payload = new Dictionary<string, object?>
        {
            ["mobile_id"] = (uint)victim.Id,
            ["is_player"] = victim.IsPlayer,
            ["map_id"] = victim.MapId,
            ["region_name"] = regionName,
            ["location"] = new Dictionary<string, int>
            {
                ["x"] = victim.Location.X,
                ["y"] = victim.Location.Y,
                ["z"] = victim.Location.Z
            }
        };

        if (killer is not null)
        {
            payload["killer_id"] = (uint)killer.Id;
        }

        if (corpse is not null)
        {
            payload["corpse_id"] = (uint)corpse.Id;
        }

        return payload;
    }

    private async Task<UOItemEntity> CreateCorpseAsync(UOMobileEntity victim, CancellationToken cancellationToken)
    {
        var corpse = new UOItemEntity
        {
            ItemId = CorpsePropertyKeys.ItemId,
            Name = string.IsNullOrWhiteSpace(victim.Name) ? "a corpse" : $"{victim.Name}'s corpse",
            MapId = victim.MapId,
            Location = victim.Location,
            Amount = victim.Body,
            Hue = victim.SkinHue
        };
        corpse.SetCustomBoolean(CorpsePropertyKeys.IsCorpse, true);
        corpse.SetCustomInteger(CorpsePropertyKeys.OwnerMobileId, victim.Id.Value);
        corpse.SetCustomInteger(CorpsePropertyKeys.DecayType, (byte)DecayType.CorpseDecay);
        await _itemService.CreateItemAsync(corpse);
        await _itemService.UpsertItemAsync(corpse);

        return corpse;
    }

    private async Task MoveLootToCorpseAsync(
        UOMobileEntity victim,
        UOItemEntity corpse,
        CancellationToken cancellationToken
    )
    {
        foreach (var layer in victim.EquippedItemIds.Keys.ToList())
        {
            if (ExcludedCorpseLayers.Contains(layer))
            {
                continue;
            }

            if (!victim.TryGetEquippedReference(layer, out var itemReference))
            {
                continue;
            }

            var item = await _itemService.GetItemAsync(itemReference.Id);

            if (item is null || !IsLootable(item))
            {
                continue;
            }

            item.SetCustomInteger(CorpsePropertyKeys.EquippedLayer, (byte)layer);
            await _itemService.UpsertItemAsync(item);
            await MoveItemIntoCorpseAsync(item, corpse, cancellationToken);
            _ = victim.UnequipItem(layer, item);
        }

        if (victim.BackpackId == Serial.Zero)
        {
            return;
        }

        var backpack = await _itemService.GetItemAsync(victim.BackpackId);

        if (backpack is null)
        {
            return;
        }

        foreach (var item in backpack.Items.ToList())
        {
            if (!IsLootable(item))
            {
                continue;
            }

            await MoveItemIntoCorpseAsync(item, corpse, cancellationToken);
        }
    }

    private static bool IsLootable(UOItemEntity item)
    {
        if (item.ItemId == 0)
        {
            return false;
        }

        if (item.TryGetCustomBoolean("immovable", out var isImmovable) && isImmovable)
        {
            return false;
        }

        if (item.TryGetCustomBoolean("system_item", out var isSystemItem) && isSystemItem)
        {
            return false;
        }

        return true;
    }

    private async Task MoveItemIntoCorpseAsync(UOItemEntity item, UOItemEntity corpse, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var position = ResolveCorpsePosition(corpse.Items.Count);
        await _itemService.MoveItemToContainerAsync(item.Id, corpse.Id, position);
        var moved = await _itemService.GetItemAsync(item.Id);

        if (moved is not null && corpse.Items.All(existing => existing.Id != moved.Id))
        {
            corpse.AddItem(moved, position);
            await _itemService.UpsertItemAsync(corpse);
        }
    }

    private void ScheduleCorpseDecay(UOItemEntity corpse)
    {
        var timerName = GetCorpseTimerName(corpse.Id);
        _timerService.UnregisterTimersByName(timerName);
        _timerService.RegisterTimer(
            timerName,
            TimeSpan.FromSeconds(Math.Max(1, _config.Game.CorpseDecaySeconds)),
            () => DecayCorpseAsync(corpse.Id, corpse.MapId, corpse.Location).GetAwaiter().GetResult(),
            repeat: false
        );
    }

    private async Task DecayCorpseAsync(Serial corpseId, int mapId, Point3D location)
    {
        _ = await _itemService.DeleteItemAsync(corpseId);
        _spatialWorldService.RemoveEntity(corpseId);
        await _spatialWorldService.BroadcastToPlayersAsync(new DeleteObjectPacket(corpseId), mapId, location);
    }

    private void EnqueueLuaDeathHook(
        UOMobileEntity victim,
        LuaBrainDeathHookType hookType,
        Serial? killerId,
        Dictionary<string, object?> payload
    )
    {
        if (victim.IsPlayer || _luaBrainRunner is null)
        {
            return;
        }

        _luaBrainRunner.EnqueueDeath(victim.Id, new LuaBrainDeathContext(hookType, killerId, payload));
    }

    private static string GetCorpseTimerName(Serial corpseId)
        => $"corpse-decay:{corpseId.Value}";

    private static Point2D ResolveCorpsePosition(int index)
    {
        var column = index % 5;
        var row = index / 5;

        return new Point2D(20 + column * 18, 20 + row * 18);
    }
}
