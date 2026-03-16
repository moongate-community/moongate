using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Default mobile service backed by persistence repositories and mobile factory.
/// </summary>
public sealed class MobileService : IMobileService
{
    private readonly ILogger _logger = Log.ForContext<MobileService>();
    private readonly IPersistenceService _persistenceService;
    private readonly IMobileFactoryService _mobileFactoryService;
    private readonly IItemFactoryService _itemFactoryService;
    private readonly IMobileTemplateService _mobileTemplateService;
    private readonly ILuaBrainRunner _luaBrainRunner;

    public MobileService(
        IPersistenceService persistenceService,
        IMobileFactoryService mobileFactoryService,
        IItemFactoryService itemFactoryService,
        IMobileTemplateService mobileTemplateService,
        ILuaBrainRunner luaBrainRunner
    )
    {
        _persistenceService = persistenceService;
        _mobileFactoryService = mobileFactoryService;
        _itemFactoryService = itemFactoryService;
        _mobileTemplateService = mobileTemplateService;
        _luaBrainRunner = luaBrainRunner;
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

        return await _persistenceService.UnitOfWork.Mobiles.GetByIdAsync(id, cancellationToken);
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
                inferredItems.Add(item);
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
}
