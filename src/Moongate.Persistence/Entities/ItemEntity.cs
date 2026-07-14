using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Interfaces;
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

    public AccountLevelType Visibility { get; set; } = AccountLevelType.User;

    public string? Description { get; set; }

    public int MapId { get; set; }

    public Point3D Position { get; set; }

    public DirectionType Direction { get; set; }

    public int Amount { get; set; }


}
