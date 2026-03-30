using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Internal.Packets;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Interfaces.Services.Timing;
using Moongate.Server.Types.Items;
using Moongate.Server.Utils;
using Moongate.UO.Data.Constants;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Races.Base;
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
    private readonly ILootGenerationService? _lootGenerationService;

    public DeathService(
        IMobileService mobileService,
        IItemService itemService,
        ISpatialWorldService spatialWorldService,
        ITimerService timerService,
        IGameEventBusService gameEventBusService,
        IFameKarmaService fameKarmaService,
        MoongateConfig config,
        ILuaBrainRunner? luaBrainRunner = null,
        ILootGenerationService? lootGenerationService = null
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
        _lootGenerationService = lootGenerationService;
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

        var corpseBody = ResolveCorpseBody(victim);
        UOItemEntity? corpse = null;

        corpse = await CreateCorpseAsync(victim, corpseBody, cancellationToken);
        await MoveLootToCorpseAsync(victim, corpse, cancellationToken);

        if (victim.IsPlayer)
        {
            await ConvertPlayerToGhostAsync(victim, cancellationToken);
            await _mobileService.CreateOrUpdateAsync(victim, cancellationToken);
        }

        if (!victim.IsPlayer)
        {
            await GenerateCorpseLootAsync(victim, corpse, cancellationToken);
        }

        _spatialWorldService.AddOrUpdateItem(corpse, corpse.MapId);
        await BroadcastVisibleCorpsePacketsAsync(corpse);
        await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
            new MobileDeathAnimationPacket(victim.Id, corpse.Id),
            victim.MapId,
            victim.Location
        );

        if (victim.IsPlayer)
        {
            await _gameEventBusService.PublishAsync(new MobileAppearanceChangedEvent(victim), cancellationToken);
        }

        if (!victim.IsPlayer)
        {
            _spatialWorldService.RemoveEntity(victim.Id);
            await _mobileService.DeleteAsync(victim.Id, cancellationToken);
        }

        ScheduleCorpseDecay(corpse);

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

    private async Task BroadcastVisibleCorpsePacketsAsync(UOItemEntity corpse)
    {
        await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
            ItemPacketHelper.CreateObjectInformationPacket(corpse, AccountType.Regular),
            corpse.MapId,
            corpse.Location
        );

        List<IGameNetworkPacket> packets = [];
        CorpsePacketHelper.EnqueueVisibleCorpsePackets(corpse, packet => packets.Add(packet));

        foreach (var packet in packets)
        {
            await _spatialWorldService.BroadcastToPlayersInUpdateRadiusAsync(
                packet,
                corpse.MapId,
                corpse.Location
            );
        }
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

    private async Task<UOItemEntity> CreateCorpseAsync(
        UOMobileEntity victim,
        int corpseBody,
        CancellationToken cancellationToken
    )
    {
        var corpse = new UOItemEntity
        {
            ItemId = CorpsePropertyKeys.ItemId,
            Name = string.IsNullOrWhiteSpace(victim.Name) ? "a corpse" : $"{victim.Name}'s corpse",
            MapId = victim.MapId,
            Location = victim.Location,
            Amount = corpseBody,
            Hue = victim.SkinHue
        };
        corpse.SetCustomBoolean(CorpsePropertyKeys.IsCorpse, true);
        corpse.SetCustomInteger(CorpsePropertyKeys.OwnerMobileId, victim.Id.Value);
        corpse.SetCustomInteger(CorpsePropertyKeys.DecayType, (byte)DecayType.CorpseDecay);
        await _itemService.CreateItemAsync(corpse);
        await _itemService.UpsertItemAsync(corpse);

        return corpse;
    }

    private async Task DecayCorpseAsync(Serial corpseId, int mapId, Point3D location)
    {
        await DeleteContainedItemsRecursiveAsync(corpseId);
        _ = await _itemService.DeleteItemAsync(corpseId);
        _spatialWorldService.RemoveEntity(corpseId);
        await _spatialWorldService.BroadcastToPlayersAsync(new DeleteObjectPacket(corpseId), mapId, location);
    }

    private async Task DeleteContainedItemsRecursiveAsync(Serial containerId)
    {
        var containedItems = await _itemService.GetItemsInContainerAsync(containerId);

        foreach (var item in containedItems)
        {
            await DeleteContainedItemsRecursiveAsync(item.Id);
            _ = await _itemService.DeleteItemAsync(item.Id);
        }
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

        _luaBrainRunner.EnqueueDeath(victim.Id, new(hookType, killerId, payload));
    }

    private async Task GenerateCorpseLootAsync(
        UOMobileEntity victim,
        UOItemEntity corpse,
        CancellationToken cancellationToken
    )
    {
        if (_lootGenerationService is null ||
            !victim.TryGetCustomString(MobileCustomParamKeys.Loot.LootTables, out var lootTablesRaw) ||
            string.IsNullOrWhiteSpace(lootTablesRaw))
        {
            return;
        }

        var lootTableIds = lootTablesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (lootTableIds.Length == 0)
        {
            return;
        }

        await _lootGenerationService.GenerateForContainerAsync(
            corpse,
            lootTableIds,
            LootGenerationMode.OnDeath,
            cancellationToken
        );
    }

    private async Task ConvertPlayerToGhostAsync(UOMobileEntity victim, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        await ClearRemainingEquipmentAsync(victim);

        victim.BaseBody = 0x00;

        var shroud = await _itemService.SpawnFromTemplateAsync("death_shroud");
        shroud.MapId = victim.MapId;
        shroud.Location = victim.Location;
        victim.AddEquippedItem(ItemLayerType.OuterTorso, shroud);
        await _itemService.UpsertItemAsync(shroud);
    }

    private static string GetCorpseTimerName(Serial corpseId)
        => $"corpse-decay:{corpseId.Value}";

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

        var backpackId = ResolveBackpackId(victim);

        if (backpackId == Serial.Zero)
        {
            return;
        }

        var backpack = await _itemService.GetItemAsync(backpackId);

        if (backpack is null || !IsLootable(backpack))
        {
            return;
        }

        await MoveItemIntoCorpseAsync(backpack, corpse, cancellationToken);
        _ = victim.UnequipItem(ItemLayerType.Backpack, backpack);
        victim.BackpackId = Serial.Zero;
    }

    private async Task ClearRemainingEquipmentAsync(UOMobileEntity victim)
    {
        foreach (var layer in victim.EquippedItemIds.Keys.ToList())
        {
            if (layer == ItemLayerType.Bank)
            {
                continue;
            }

            if (victim.TryGetEquippedReference(layer, out var itemReference))
            {
                var item = await _itemService.GetItemAsync(itemReference.Id);
                _ = victim.UnequipItem(layer, item);

                continue;
            }

            _ = victim.UnequipItem(layer);
        }
    }

    private static Point2D ResolveCorpsePosition(int index)
    {
        var column = index % 5;
        var row = index / 5;

        return new(20 + column * 18, 20 + row * 18);
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

    private static Serial ResolveBackpackId(UOMobileEntity victim)
    {
        if (victim.BackpackId != Serial.Zero)
        {
            return victim.BackpackId;
        }

        return victim.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId)
                   ? equippedBackpackId
                   : Serial.Zero;
    }

    private static int ResolveCorpseBody(UOMobileEntity victim)
    {
        if (victim.BaseBody is { } baseBody && baseBody != 0x00)
        {
            return baseBody;
        }

        var race = victim.Race ?? (Race.Races.Length > 0 ? Race.Races[0] : null);

        if (race is not null)
        {
            return race.AliveBody(victim.Gender == GenderType.Female);
        }

        return victim.Body;
    }
}
