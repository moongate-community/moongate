using FreeSql.DataAnnotations;
using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Entities.World;

[Table(Name = "world.characters"), Index("ux_characters_name", nameof(Name), true)]
public class CharacterEntity
{

    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }


    [Column(MapType = typeof(long))]
    public Serial AccountId { get; set; }

    public string Name { get; set; }

}
