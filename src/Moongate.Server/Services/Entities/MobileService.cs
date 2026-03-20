using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.World;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;
using System.Globalization;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Default mobile service backed by persistence repositories and mobile factory.
/// </summary>
public sealed class MobileService : IMobileService
{
    private const string MountedDisplayItemIdKey = "mounted_display_item_id";
    private const int MountInteractionRange = 2;
    private readonly ILogger _logger = Log.ForContext<MobileService>();
    private readonly IPersistenceService _persistenceService;
    private readonly IMobileFactoryService _mobileFactoryService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly ILuaBrainRunner _luaBrainRunner;
    private readonly MountTileData _mountTileData;

    public MobileService(
        IPersistenceService persistenceService,
        IMobileFactoryService mobileFactoryService,
        IItemFactoryService itemFactoryService,
        IMobileTemplateService mobileTemplateService,
        ILuaBrainRunner luaBrainRunner,
        MountTileData mountTileData
    )
    {
        _persistenceService = persistenceService;
        _mobileFactoryService = mobileFactoryService;
        _itemFactoryService = itemFactoryService;
        _mobileTemplateService = mobileTemplateService;
        _luaBrainRunner = luaBrainRunner;
        _mountTileData = mountTileData;
    }

    /// <inheritdoc />
    public async Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mobile);

        if (mobile.Id == Serial.Zero)
        {
            mobile.Id = _persistenceService.UnitOfWork.AllocateNextMobileId();
        }

        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
    {
        if (id == Serial.Zero)
        {
            return false;
        }

        var removed = await _persistenceService.UnitOfWork.Mobiles.RemoveAsync(id, cancellationToken);

        if (removed)
        {
            _luaBrainRunner.Unregister(id);
        }

        return removed;
    }

    /// <inheritdoc />
    public async Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
    {
        if (id == Serial.Zero)
        {
            return null;
        }

        var mobile = await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(id, cancellationToken);

        if (mobile is not null)
        {
            ApplyMountableState(mobile);
        }

        return mobile;
    }

    /// <inheritdoc />
    public async Task<bool> TryMountAsync(Serial riderId, Serial mountId, CancellationToken cancellationToken = default)
    {
        if (riderId == Serial.Zero || mountId == Serial.Zero || riderId == mountId)
        {
            _logger.Debug(
                "TryMountAsync rejected invalid ids Rider={RiderId} Mount={MountId}",
                riderId,
                mountId
            );
            return false;
        }

        var rider = await GetAsync(riderId, cancellationToken);
        var mount = await GetAsync(mountId, cancellationToken);

        if (rider is null || mount is null)
        {
            _logger.Debug(
                "TryMountAsync missing entities RiderFound={RiderFound} MountFound={MountFound} Rider={RiderId} Mount={MountId}",
                rider is not null,
                mount is not null,
                riderId,
                mountId
            );
            return false;
        }

        if (!mount.IsMountable)
        {
            _logger.Debug(
                "TryMountAsync rejected non-mountable target Rider={RiderId} Mount={MountId} MountedDisplayItemId={MountedDisplayItemId}",
                riderId,
                mountId,
                ResolveMountedDisplayItemId(mount)
            );
            return false;
        }

        if (rider.MapId != mount.MapId || !rider.Location.InRange(mount.Location, MountInteractionRange))
        {
            _logger.Debug(
                "TryMountAsync rejected range/map Rider={RiderId} Mount={MountId} RiderMap={RiderMap} MountMap={MountMap} RiderLocation={RiderLocation} MountLocation={MountLocation}",
                riderId,
                mountId,
                rider.MapId,
                mount.MapId,
                rider.Location,
                mount.Location
            );
            return false;
        }

        if (
            rider.MountedMobileId != Serial.Zero ||
            rider.RiderMobileId != Serial.Zero ||
            mount.MountedMobileId != Serial.Zero ||
            mount.RiderMobileId != Serial.Zero
        )
        {
            _logger.Debug(
                "TryMountAsync rejected occupied state Rider={RiderId} Mount={MountId} RiderMountedMobileId={RiderMountedMobileId} RiderMobileId={RiderMobileId} MountMountedMobileId={MountMountedMobileId} MountRiderMobileId={MountRiderMobileId}",
                riderId,
                mountId,
                rider.MountedMobileId,
                rider.RiderMobileId,
                mount.MountedMobileId,
                mount.RiderMobileId
            );
            return false;
        }

        rider.MountedMobileId = mount.Id;
        rider.MountedDisplayItemId = ResolveMountedDisplayItemId(mount);
        mount.RiderMobileId = rider.Id;

        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(rider, cancellationToken);
        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mount, cancellationToken);

        _logger.Debug(
            "TryMountAsync linked Rider={RiderId} Mount={MountId} MountedDisplayItemId={MountedDisplayItemId}",
            riderId,
            mountId,
            rider.MountedDisplayItemId
        );

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> DismountAsync(Serial riderId, CancellationToken cancellationToken = default)
    {
        if (riderId == Serial.Zero)
        {
            return false;
        }

        var rider = await GetAsync(riderId, cancellationToken);

        if (rider is null || rider.MountedMobileId == Serial.Zero)
        {
            return false;
        }

        var mountId = rider.MountedMobileId;
        rider.MountedMobileId = Serial.Zero;
        rider.MountedDisplayItemId = 0;
        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(rider, cancellationToken);

        var mount = await GetAsync(mountId, cancellationToken);

        if (mount is not null && mount.RiderMobileId == riderId)
        {
            mount.RiderMobileId = Serial.Zero;
            await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mount, cancellationToken);
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
        int mapId,
        int sectorX,
        int sectorY,
        CancellationToken cancellationToken = default
    )
    {
        var minX = sectorX * MapSectorConsts.SectorSize;
        var maxX = minX + MapSectorConsts.SectorSize;
        var minY = sectorY * MapSectorConsts.SectorSize;
        var maxY = minY + MapSectorConsts.SectorSize;

        var mobiles = await _persistenceService.UnitOfWork.Mobiles.QueryAsync(
                          mobile => !mobile.IsPlayer &&
                                    mobile.MapId == mapId &&
                                    mobile.Location.X >= minX &&
                                    mobile.Location.X < maxX &&
                                    mobile.Location.Y >= minY &&
                                    mobile.Location.Y < maxY,
                          static mobile => mobile,
                          cancellationToken
                      );

        var result = mobiles.ToList();
        await HydrateMobileEquipmentRuntimeAsync(result, cancellationToken);
        ApplyMountableState(result);

        return result;
    }

    /// <inheritdoc />
    public async Task<UOMobileEntity> SpawnFromTemplateAsync(
        string templateId,
        Point3D location,
        int mapId,
        Serial? accountId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        var mobile = _mobileFactoryService.CreateMobileFromTemplate(templateId, accountId);
        mobile.Location = location;
        mobile.MapId = mapId;
        ApplyMountableState(mobile);
        RegisterBrainIfConfigured(templateId, mobile);

        await CreateOrUpdateAsync(mobile, cancellationToken);
        await ApplyTemplateEquipmentAsync(templateId, mobile, cancellationToken);

        return mobile;
    }

    /// <inheritdoc />
    public async Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
        string templateId,
        Point3D location,
        int mapId,
        Serial? accountId = null,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return (false, null);
        }

        if (!_mobileTemplateService.TryGet(templateId, out var definition) || definition is null)
        {
            return (false, null);
        }

        var mobile = await SpawnFromTemplateAsync(templateId, location, mapId, accountId, cancellationToken);

        return (true, mobile);
    }

    private static int ResolveMountedDisplayItemId(UOMobileEntity mount)
    {
        if (mount.TryGetCustomInteger(MountedDisplayItemIdKey, out var mountedDisplayItemId))
        {
            return (int)mountedDisplayItemId;
        }

        if (mount.TryGetCustomString(MountedDisplayItemIdKey, out var mountedDisplayItemIdRaw) &&
            TryParseDisplayItemId(mountedDisplayItemIdRaw, out var parsedMountedDisplayItemId))
        {
            return parsedMountedDisplayItemId;
        }

        return mount.Body;
    }

    private static bool TryParseDisplayItemId(string? value, out int itemId)
    {
        itemId = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out itemId);
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId);
    }

    private void ApplyMountableState(IEnumerable<UOMobileEntity> mobiles)
    {
        foreach (var mobile in mobiles)
        {
            ApplyMountableState(mobile);
        }
    }

    private void ApplyMountableState(UOMobileEntity mobile)
    {
        var mountedDisplayItemId = ResolveMountedDisplayItemId(mobile);
        mobile.IsMountable = mountedDisplayItemId > 0 && _mountTileData.Contains(mountedDisplayItemId);
    }

    private async Task ApplyTemplateEquipmentAsync(
        string templateId,
        UOMobileEntity mobile,
        CancellationToken cancellationToken
    )
    {
        if (!_mobileTemplateService.TryGet(templateId, out var definition) || definition is null)
        {
            return;
        }

        foreach (var fixedEquipment in definition.FixedEquipment)
        {
            await TryEquipTemplateItemAsync(mobile, fixedEquipment.Layer, fixedEquipment.ItemTemplateId, cancellationToken);
        }

        foreach (var randomPool in definition.RandomEquipment)
        {
            if (randomPool.SpawnChance < 1.0f && Random.Shared.NextDouble() > randomPool.SpawnChance)
            {
                continue;
            }

            var selected = SelectRandomEquipment(randomPool);

            if (selected is null)
            {
                continue;
            }

            await TryEquipTemplateItemAsync(mobile, randomPool.Layer, selected.ItemTemplateId, cancellationToken);
        }

        await _persistenceService.UnitOfWork.Mobiles.UpsertAsync(mobile, cancellationToken);
    }

    private async Task HydrateMobileEquipmentRuntimeAsync(
        UOMobileEntity mobile,
        CancellationToken cancellationToken = default
    )
        => await HydrateMobileEquipmentRuntimeAsync([mobile], cancellationToken);

    private async Task HydrateMobileEquipmentRuntimeAsync(
        IReadOnlyList<UOMobileEntity> mobiles,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(mobiles);

        if (mobiles.Count == 0)
        {
            return;
        }

        var mobilesWithEquipment = mobiles
                                   .Where(mobile => mobile.EquippedItemIds.Count > 0)
                                   .ToList();

        foreach (var mobile in mobiles.Except(mobilesWithEquipment))
        {
            mobile.HydrateEquipmentRuntime([]);
        }

        if (mobilesWithEquipment.Count == 0)
        {
            return;
        }

        var mobileIds = mobilesWithEquipment
                        .Select(mobile => mobile.Id)
                        .ToHashSet();
        var equippedItems = await _persistenceService.UnitOfWork.Items.QueryAsync(
                                item => item.EquippedMobileId != Serial.Zero &&
                                        item.EquippedLayer is not null &&
                                        mobileIds.Contains(item.EquippedMobileId),
                                static item => item,
                                cancellationToken
                            );
        var equippedItemsByMobileId = equippedItems
                                      .GroupBy(item => item.EquippedMobileId)
                                      .ToDictionary(
                                          group => group.Key,
                                          group => group.ToDictionary(static item => item.Id, static item => item)
                                      );

        foreach (var mobile in mobilesWithEquipment)
        {
            equippedItemsByMobileId.TryGetValue(mobile.Id, out var hydratedItemsById);
            hydratedItemsById ??= [];
            var inferredItems = new List<UOItemEntity>(mobile.EquippedItemIds.Count);

            foreach (var (layer, itemId) in mobile.EquippedItemIds)
            {
                if (hydratedItemsById.ContainsKey(itemId))
                {
                    continue;
                }

                var item = await _persistenceService.UnitOfWork.Items.GetByIdAsync(itemId, cancellationToken);

                if (item is null)
                {
                    continue;
                }

                item.EquippedMobileId = mobile.Id;
                item.EquippedLayer = layer;
                BackfillTemplateCombatMetadata(item);
                inferredItems.Add(item);
            }

            foreach (var item in hydratedItemsById.Values)
            {
                BackfillTemplateCombatMetadata(item);
            }

            if (inferredItems.Count > 0)
            {
                mobile.HydrateEquipmentRuntime([.. hydratedItemsById.Values, .. inferredItems]);

                continue;
            }

            mobile.HydrateEquipmentRuntime(hydratedItemsById.Values);
        }
    }

    private void RegisterBrainIfConfigured(string templateId, UOMobileEntity mobile)
    {
        if (!_mobileTemplateService.TryGet(templateId, out var definition) || definition is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.Brain))
        {
            mobile.BrainId = null;

            return;
        }

        var resolvedBrainId = definition.Brain.Trim();

        if (string.Equals(resolvedBrainId, "none", StringComparison.OrdinalIgnoreCase))
        {
            mobile.BrainId = null;

            return;
        }

        mobile.BrainId = resolvedBrainId;
    }

    private static MobileWeightedEquipmentItemTemplate? SelectRandomEquipment(MobileRandomEquipmentPoolTemplate pool)
    {
        if (pool.Items.Count == 0)
        {
            return null;
        }

        var validItems = pool.Items.Where(static item => item.Weight > 0).ToArray();

        if (validItems.Length == 0)
        {
            return null;
        }

        var totalWeight = validItems.Sum(static item => item.Weight);
        var roll = Random.Shared.Next(1, totalWeight + 1);
        var accumulator = 0;

        foreach (var item in validItems)
        {
            accumulator += item.Weight;

            if (roll <= accumulator)
            {
                return item;
            }
        }

        return validItems[^1];
    }

    private async Task TryEquipTemplateItemAsync(
        UOMobileEntity mobile,
        ItemLayerType layer,
        string itemTemplateId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(itemTemplateId))
        {
            return;
        }

        try
        {
            var item = _itemFactoryService.CreateItemFromTemplate(itemTemplateId);
            item.MapId = mobile.MapId;
            mobile.AddEquippedItem(layer, item);

            if (layer == ItemLayerType.Backpack)
            {
                mobile.BackpackId = item.Id;
            }

            await _persistenceService.UnitOfWork.Items.UpsertAsync(item, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Failed to equip template item {ItemTemplateId} on mobile {MobileId} (layer={Layer})",
                itemTemplateId,
                mobile.Id,
                layer
            );
        }
    }

    private void BackfillTemplateCombatMetadata(UOItemEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!TryResolveItemTemplate(item, out var template) || template is null)
        {
            return;
        }

        item.WeaponSkill ??= template.WeaponSkill;

        if (item.AmmoItemId is null)
        {
            item.AmmoItemId = ToNullableItemId(template.Ammo);
        }

        if (item.AmmoEffectId is null)
        {
            item.AmmoEffectId = ToNullableItemId(template.AmmoFx);
        }

        if (template.BaseRange <= 0 && template.MaxRange <= 0)
        {
            return;
        }

        item.CombatStats ??= new ItemCombatStats();

        if (item.CombatStats.RangeMin <= 0)
        {
            item.CombatStats.RangeMin = template.BaseRange;
        }

        if (item.CombatStats.RangeMax <= 0)
        {
            item.CombatStats.RangeMax = template.MaxRange;
        }
    }

    private bool TryResolveItemTemplate(UOItemEntity item, out ItemTemplateDefinition? template)
    {
        template = null;

        if (!item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) ||
            string.IsNullOrWhiteSpace(templateId))
        {
            return false;
        }

        return _itemFactoryService.TryGetItemTemplate(templateId.Trim(), out template);
    }

    private static int? ToNullableItemId(int itemId)
        => itemId > 0 ? itemId : null;
}
