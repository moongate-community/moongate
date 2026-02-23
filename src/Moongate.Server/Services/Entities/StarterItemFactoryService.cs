using Moongate.Server.Data.Entities;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Persistence;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Tiles;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Entities;

/// <summary>
/// Creates starter inventory and equipment items for newly created characters.
/// </summary>
public sealed class StarterItemFactoryService : IStarterItemFactoryService
{
    private const int GoldItemId = 0x0EED;
    private const int ShirtItemId = 0x1517;
    private const int PantsItemId = 0x152E;
    private const int ShoesItemId = 0x170F;

    private readonly IItemFactoryService _itemFactoryService;
    private readonly IPersistenceService _persistenceService;

    public StarterItemFactoryService(IItemFactoryService itemFactoryService, IPersistenceService persistenceService)
    {
        _itemFactoryService = itemFactoryService;
        _persistenceService = persistenceService;
    }

    /// <inheritdoc />
    public UOItemEntity CreateStarterBackpack(Serial mobileId, StarterProfileContext profileContext)
    {
        _ = profileContext;
        var backpack = _itemFactoryService.GetNewBackpack();
        backpack.EquippedMobileId = mobileId;
        backpack.EquippedLayer = ItemLayerType.Backpack;
        backpack.ParentContainerId = Serial.Zero;
        backpack.ContainerPosition = Point2D.Zero;
        backpack.Location = Point3D.Zero;

        return backpack;
    }

    /// <inheritdoc />
    public UOItemEntity CreateStarterEquipment(Serial mobileId, ItemLayerType layer, StarterProfileContext profileContext)
    {
        _ = profileContext;
        var itemId = layer switch
        {
            ItemLayerType.Shirt => ShirtItemId,
            ItemLayerType.Pants => PantsItemId,
            ItemLayerType.Shoes => ShoesItemId,
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unsupported starter equipment layer.")
        };

        var itemFromTile = TileData.ItemTable[itemId];

        return new()
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            Name = itemFromTile.Name,
            Weight = itemFromTile.Weight,
            Amount = 1,
            IsStackable = false,
            Rarity = ItemRarity.Common,
            ItemId = itemId,
            Hue = 0,
            Location = Point3D.Zero,
            ParentContainerId = Serial.Zero,
            ContainerPosition = Point2D.Zero,
            EquippedMobileId = mobileId,
            EquippedLayer = layer
        };
    }

    /// <inheritdoc />
    public UOItemEntity CreateStarterGold(
        Serial containerId,
        Point2D containerPosition,
        int quantity,
        StarterProfileContext profileContext
    )
    {
        _ = profileContext;
        quantity = Math.Max(1, quantity);
        var itemFromTile = TileData.ItemTable[GoldItemId];

        return new()
        {
            Id = _persistenceService.UnitOfWork.AllocateNextItemId(),
            Name = itemFromTile.Name,
            Weight = itemFromTile.Weight,
            Amount = quantity,
            IsStackable = true,
            Rarity = ItemRarity.Common,
            ItemId = GoldItemId,
            Hue = 0,
            Location = Point3D.Zero,
            ParentContainerId = containerId,
            ContainerPosition = containerPosition,
            EquippedMobileId = Serial.Zero,
            EquippedLayer = null
        };
    }
}
