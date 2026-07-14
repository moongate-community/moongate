using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Interfaces;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Persistence.Entities;

public class ItemEntity : ISerialIdEntity, IPositionEntity
{
    public Serial Id { get; set; }

    public int ItemId { get; set; }

    public Hue Hue { get; set; }

    public int? GumpId { get; set; }

    public string Name { get; set; }

    public string ScriptId { get; set; } = string.Empty;

    public ItemRarityType Rarity { get; set; } = ItemRarityType.Common;

    public AccountLevelType Visibility { get; set; } = AccountLevelType.Player;

    public string? Description { get; set; }

    public int MapId { get; set; }

    public Point3D Position { get; set; }

    public DirectionType Direction { get; set; }

    public int Amount { get; set; }

    /// <summary>Owning container serial when the item is inside a container; <see cref="Serial.Zero" /> otherwise.</summary>
    public Serial ParentContainerId { get; set; }

    /// <summary>Item position inside its parent container.</summary>
    public Point2D ContainerPosition { get; set; }

    /// <summary>Wearer serial when the item is equipped; <see cref="Serial.Zero" /> otherwise.</summary>
    public Serial EquippedMobileId { get; set; }

    /// <summary>Equipped layer when the item is worn; <c>null</c> otherwise.</summary>
    public LayerType? EquippedLayer { get; set; }

    /// <summary>Serials of the items this container holds; each contained item is its own top-level store record.</summary>
    public List<Serial> ContainedItemIds { get; set; } = [];
}
