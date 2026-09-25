using FreeSql.DataAnnotations;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     A mobile of the world: a player character or an NPC. Both share the same serial range and the same state; an
///     NPC is the one without an <see cref="AccountId" />.
/// </summary>
/// <remarks>
///     Names are not unique here, because NPCs repeat them; the uniqueness of player character names is checked when a
///     character is created.
/// </remarks>
[Table(Name = "world.mobiles")]
public class MobileEntity : IMoongateEntity
{

    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }


    /// <summary>
    ///     The account of a player character; <see langword="null" /> for an NPC.
    /// </summary>
    [Column(MapType = typeof(long?), IsNullable = true)]
    public Serial? AccountId { get; set; }

    /// <summary>
    ///     Gets whether this mobile is an NPC, that is, it belongs to no account.
    /// </summary>
    [Column(IsIgnore = true)]
    public bool IsNpc => AccountId is null;

    public string Name { get; set; }

    [Column(MapType = typeof(byte))]
    public GenderType Gender { get; set; }

    [Column(MapType = typeof(byte))]
    public RaceType Race { get; set; }

    /// <summary>
    ///     The body id, which follows race and gender (for example 400 for a male human).
    /// </summary>
    public int Body { get; set; }

    public Hue SkinHue { get; set; }

    public int Strength { get; set; }

    public int Dexterity { get; set; }

    public int Intelligence { get; set; }

    /// <summary>
    ///     The item id of the hair style; 0 means no hair.
    /// </summary>
    public int HairStyle { get; set; }

    public Hue HairHue { get; set; }

    /// <summary>
    ///     The item id of the beard style; 0 means no beard.
    /// </summary>
    public int BeardStyle { get; set; }

    public Hue BeardHue { get; set; }

    /// <summary>
    ///     The skills the mobile has; a skill missing from the list is at 0 with the default cap and lock.
    /// </summary>
    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public List<MobileSkill> Skills { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    /// <summary>
    ///     Stored as its byte value: <see cref="MapType" /> is byte-backed, which FreeSql cannot write to an int column.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public MapType Map { get; set; }

    /// <summary>
    ///     Gets or sets <see cref="X" />, <see cref="Y" /> and <see cref="Z" /> together. It is not a column: the three
    ///     coordinates are stored apart so the database can filter and index them.
    /// </summary>
    [Column(IsIgnore = true)]
    public Point3D Location
    {
        get => new(X, Y, Z);
        set => (X, Y, Z) = (value.X, value.Y, value.Z);
    }
}
