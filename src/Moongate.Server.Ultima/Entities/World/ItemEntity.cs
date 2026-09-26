using FreeSql.DataAnnotations;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     An item of the world: on the ground, inside a container item or worn by a mobile, never in two places.
/// </summary>
/// <remarks>
///     The three location groups are separate columns so the database can check that exactly one is set; move an item
///     only through <see cref="PlaceOnGround" />, <see cref="PutInContainer" /> and <see cref="Equip" />, which clear
///     the other two. Only what differs from the template is stored: a null <see cref="Name" />, <see cref="Movable" />
///     or <see cref="Visibility" /> means the template's value.
/// </remarks>
[Table(Name = "world.items")]
public class ItemEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The id of the item template the item was made from, such as <c>orcspawn</c>.
    /// </summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    ///     The graphic; it can differ from the template's, as for an opened door.
    /// </summary>
    public int ItemId { get; set; }

    public Hue Hue { get; set; }

    public int Amount { get; set; } = 1;

    /// <summary>
    ///     The name when it differs from the template's; null uses the template's or tiledata's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The <see cref="Moongate.Ultima.Types.MapType" /> value when the item is on the ground.
    /// </summary>
    public byte? Map { get; set; }

    public int? X { get; set; }

    public int? Y { get; set; }

    public short? Z { get; set; }

    /// <summary>
    ///     The container item holding this item.
    /// </summary>
    [Column(MapType = typeof(long?), IsNullable = true)]
    public Serial? ContainerId { get; set; }

    public short? GridX { get; set; }

    public short? GridY { get; set; }

    /// <summary>
    ///     The mobile wearing this item.
    /// </summary>
    [Column(MapType = typeof(long?), IsNullable = true)]
    public Serial? MobileId { get; set; }

    /// <summary>
    ///     The <see cref="Moongate.Ultima.Types.LayerType" /> value when the item is worn.
    /// </summary>
    public byte? Layer { get; set; }

    /// <summary>
    ///     Whether the item can be picked up, when it differs from the template's.
    /// </summary>
    public bool? Movable { get; set; }

    /// <summary>
    ///     The <see cref="AccountType" /> value that sees the item, when it differs from the template's.
    /// </summary>
    public byte? Visibility { get; set; }

    /// <summary>
    ///     When the item decays, in UTC; null never decays.
    /// </summary>
    public DateTime? DecayAt { get; set; }

    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public ItemProps? Props { get; set; }

    [Column(IsIgnore = true)]
    public ItemLocationType Location =>
        Map is not null ? ItemLocationType.Ground :
        ContainerId is not null ? ItemLocationType.Container :
        MobileId is not null ? ItemLocationType.Equipped :
        ItemLocationType.None;

    [Column(IsIgnore = true)]
    public MapType? GroundMap => Map is { } map ? (MapType)map : null;

    [Column(IsIgnore = true)]
    public Point3D? GroundLocation => Map is null ? null : new Point3D(X!.Value, Y!.Value, Z!.Value);

    [Column(IsIgnore = true)]
    public LayerType? WornLayer => Layer is { } layer ? (LayerType)layer : null;

    [Column(IsIgnore = true)]
    public AccountType? VisibilityType
    {
        get
        {
            return Visibility is { } visibility ? (AccountType)visibility : null;
        }
        set
        {
            Visibility = value is { } type ? (byte)type : null;
        }
    }

    /// <summary>
    ///     Puts the item on the ground of <paramref name="map" /> at <paramref name="location" />.
    /// </summary>
    public void PlaceOnGround(MapType map, Point3D location)
    {
        ClearLocation();
        Map = (byte)map;
        X = location.X;
        Y = location.Y;
        Z = (short)location.Z;
    }

    /// <summary>
    ///     Puts the item inside the container item <paramref name="containerId" />, at a position in its gump.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     <paramref name="containerId" /> is not an item serial, or is this item.
    /// </exception>
    public void PutInContainer(Serial containerId, short gridX, short gridY)
    {
        if (!containerId.IsItem || containerId == Id)
        {
            throw new ArgumentException($"{containerId} is not another item that can hold this one.", nameof(containerId));
        }

        ClearLocation();
        ContainerId = containerId;
        GridX = gridX;
        GridY = gridY;
    }

    /// <summary>
    ///     Makes mobile <paramref name="mobileId" /> wear the item on <paramref name="layer" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     <paramref name="mobileId" /> is not a mobile serial, or <paramref name="layer" /> is
    ///     <see cref="LayerType.None" />.
    /// </exception>
    public void Equip(Serial mobileId, LayerType layer)
    {
        if (!mobileId.IsMobile)
        {
            throw new ArgumentException($"{mobileId} is not a mobile serial.", nameof(mobileId));
        }

        if (layer == LayerType.None)
        {
            throw new ArgumentException("An item cannot be worn on the None layer.", nameof(layer));
        }

        ClearLocation();
        MobileId = mobileId;
        Layer = (byte)layer;
    }

    private void ClearLocation()
    {
        Map = null;
        X = null;
        Y = null;
        Z = null;
        ContainerId = null;
        GridX = null;
        GridY = null;
        MobileId = null;
        Layer = null;
    }
}
