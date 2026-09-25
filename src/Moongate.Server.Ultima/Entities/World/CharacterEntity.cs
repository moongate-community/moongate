using FreeSql.DataAnnotations;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Characters;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Entities.World;

[Table(Name = "world.characters"), Index("ux_characters_name", nameof(Name), true)]
public class CharacterEntity : IMoongateEntity
{

    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }


    [Column(MapType = typeof(long))]
    public Serial AccountId { get; set; }

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
    ///     The skills the character has; a skill missing from the list is at 0 with the default cap and lock.
    /// </summary>
    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public List<CharacterSkill> Skills { get; set; } = [];

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
