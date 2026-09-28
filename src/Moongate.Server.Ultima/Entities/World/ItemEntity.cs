using FreeSql.DataAnnotations;
using Moongate.Core.Attributes;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.Internal;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Templates;
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
[SerialRange(Serial.MinItem, Serial.MaxItem)]
public class ItemEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The id of the item template the item was made from, such as <c>orcspawn</c>.
    /// </summary>
    [Column(IsNullable = false)]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>
    ///     The graphic; it can differ from the template's, as for an opened door.
    /// </summary>
    public int ItemId { get; set; }

    public Hue Hue { get; set; }

    public int Amount { get; set; } = 1;

    /// <summary>
    ///     The rarity picked when the item was made.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public ItemRarityType Rarity { get; set; }

    /// <summary>
    ///     The name when it differs from the template's; null uses the template's or tiledata's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The <see cref="Moongate.Ultima.Types.MapType" /> value when the item is on the ground.
    /// </summary>
    [Column(MapType = typeof(byte?))]
    public MapType? Map { get; set; }

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
    [Column(MapType = typeof(byte?))]
    public LayerType? Layer { get; set; }

    /// <summary>
    ///     Whether the item can be picked up, when it differs from the template's.
    /// </summary>
    public bool? Movable { get; set; }

    /// <summary>
    ///     The <see cref="AccountType" /> value that sees the item, when it differs from the template's.
    /// </summary>
    [Column(MapType = typeof(byte?))]
    public AccountType? Visibility { get; set; }

    /// <summary>
    ///     When the item decays, in UTC; null never decays.
    /// </summary>
    public DateTime? DecayAt { get; set; }

    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public Dictionary<string, object?>? Props { get; set; }

    [Column(IsIgnore = true)]
    public ItemLocationType Location
        => Map is not null ? ItemLocationType.Ground :
            ContainerId is not null ? ItemLocationType.Container :
            MobileId is not null ? ItemLocationType.Equipped :
            ItemLocationType.None;

    [Column(IsIgnore = true)]
    public Point3D? GroundLocation => Map is null ? null : new Point3D(X!.Value, Y!.Value, Z!.Value);

    [Column(IsIgnore = true)]
    public Point2D? GridLocation => ContainerId is null ? null : new Point2D(GridX!.Value, GridY!.Value);

    /// <summary>
    ///     Puts the item on the ground of <paramref name="map" /> at <paramref name="location" />.
    /// </summary>
    public void PlaceOnGround(MapType map, Point3D location)
    {
        ClearLocation();
        Map = map;
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
    /// <exception cref="ArgumentOutOfRangeException">
    ///     A coordinate of <paramref name="gridLocation" /> does not fit the 16-bit grid columns.
    /// </exception>
    public void PutInContainer(Serial containerId, Point2D gridLocation)
    {
        if (!containerId.IsItem || containerId == Id)
        {
            throw new ArgumentException($"{containerId} is not another item that can hold this one.", nameof(containerId));
        }

        if (gridLocation.X is < short.MinValue or > short.MaxValue || gridLocation.Y is < short.MinValue or > short.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gridLocation),
                gridLocation,
                "A grid position must fit in 16 bits."
            );
        }

        ClearLocation();
        ContainerId = containerId;
        GridX = (short)gridLocation.X;
        GridY = (short)gridLocation.Y;
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
        Layer = layer;
    }

    /// <summary>
    ///     Sets the prop <paramref name="key" />, such as <see cref="ItemPropKeys.Quality" />; null removes it.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The key is empty, or the value is not a string, a number, a bool or an enum.
    /// </exception>
    public void SetProp(string key, object? value)
    {
        Props = PropsDictionary.Set(Props, key, value);
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> as <typeparamref name="T" />, or <paramref name="defaultValue" /> when
    ///     the item does not have it.
    /// </summary>
    /// <exception cref="InvalidCastException">The prop holds a value that does not convert to <typeparamref name="T" />.</exception>
    public T GetProp<T>(string key, T defaultValue = default!)
    {
        return TryGetProp<T>(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> as <typeparamref name="T" />; false when the item does not have it.
    /// </summary>
    /// <exception cref="InvalidCastException">The prop holds a value that does not convert to <typeparamref name="T" />.</exception>
    public bool TryGetProp<T>(string key, out T value)
    {
        return PropsDictionary.TryGet(Props, key, out value);
    }

    /// <summary>
    ///     Removes the prop <paramref name="key" />; false when the item did not have it.
    /// </summary>
    public bool RemoveProp(string key)
    {
        Props = PropsDictionary.Remove(Props, key, out var removed);

        return removed;
    }

    /// <summary>
    ///     A one-line description for logs and debugging: serial, its own name or the template id, graphic, amount above 1
    ///     and where the item is. For the name a player sees use <c>ItemEntityExtensions.DisplayName</c>.
    /// </summary>
    public override string ToString()
    {
        var amount = Amount > 1 ? $" x{Amount}" : "";
        var where = Location switch
        {
            ItemLocationType.Container => $"in {ContainerId}",
            ItemLocationType.Equipped => $"on {MobileId} layer {Layer}",
            ItemLocationType.Ground => $"at {Map} {GroundLocation}",
            _ => "nowhere"
        };

        return $"{Id} \"{Name ?? TemplateId}\" (0x{ItemId:X4}){amount} {where}";
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
