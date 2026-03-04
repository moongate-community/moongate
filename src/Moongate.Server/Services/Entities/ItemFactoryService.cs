using System.Globalization;
using Moongate.Core.Extensions.Strings;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;
using Serilog;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Creates item entities from template definitions and fallback defaults.
/// </summary>
public sealed class ItemFactoryService : IItemFactoryService
{
    private const int BackpackItemId = 0x0E75;

    private readonly ILogger _logger = Log.ForContext<ItemFactoryService>();
    private readonly IItemTemplateService _itemTemplateService;
    private readonly IPersistenceService _persistenceService;

    public ItemFactoryService(IItemTemplateService itemTemplateService, IPersistenceService persistenceService)
    {
        _itemTemplateService = itemTemplateService;
        _persistenceService = persistenceService;
    }

    /// <inheritdoc />
    public bool TryGetItemTemplate(string itemTemplateId, out ItemTemplateDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(itemTemplateId))
        {
            return false;
        }

        var normalizedTemplateId = itemTemplateId.Trim();

        if (_itemTemplateService.TryGet(normalizedTemplateId, out definition))
        {
            return true;
        }

        var snakeCaseTemplateId = normalizedTemplateId.ToSnakeCase();

        if (!string.Equals(snakeCaseTemplateId, normalizedTemplateId, StringComparison.Ordinal) &&
            _itemTemplateService.TryGet(snakeCaseTemplateId, out definition))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public UOItemEntity CreateItemFromTemplate(string itemTemplateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemTemplateId);

        if (!TryGetItemTemplate(itemTemplateId, out var template) || template is null)
        {
            throw new InvalidOperationException($"Item template '{itemTemplateId}' not found.");
        }

        var item = new UOItemEntity
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            Name = template.Name,
            Weight = (int)template.Weight,
            Amount = 1,
            Rarity = ItemRarity.Common,
            ItemId = ParseItemId(template.ItemId),
            Hue = template.Hue.Resolve(),
            GumpId = ParseOptionalInt(template.GumpId),
            ScriptId = template.ScriptId,
            Location = Point3D.Zero,
            ParentContainerId = Serial.Zero,
            ContainerPosition = Point2D.Zero,
            EquippedMobileId = Serial.Zero,
            EquippedLayer = null
        };



        var itemFromTile = TileData.ItemTable[item.ItemId];

        if (string.IsNullOrEmpty(item.Name))
        {
            item.Name = itemFromTile.Name;
        }

        item.IsStackable = itemFromTile[UOTileFlag.Generic];

        if (item.Weight == 0)
        {
            item.Weight = itemFromTile.Weight;
        }

        return item;
    }

    /// <inheritdoc />
    public UOItemEntity GetNewBackpack()
    {
        if (_itemTemplateService.TryGet("backpack", out _))
        {
            return CreateItemFromTemplate("backpack");
        }

        _logger.Warning("Backpack template not found. Using hardcoded fallback backpack item.");

        return new()
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            Name = "Backpack",
            Weight = 0,
            Amount = 1,
            IsStackable = false,
            Rarity = ItemRarity.Common,
            ItemId = BackpackItemId,
            Hue = 0,
            ScriptId = "none",
            Location = Point3D.Zero,
            ParentContainerId = Serial.Zero,
            ContainerPosition = Point2D.Zero,
            EquippedMobileId = Serial.Zero,
            EquippedLayer = null
        };
    }

    private static int ParseItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(trimmed, CultureInfo.InvariantCulture);
    }

    private static int? ParseOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(trimmed.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.Parse(trimmed, CultureInfo.InvariantCulture);
    }

}
