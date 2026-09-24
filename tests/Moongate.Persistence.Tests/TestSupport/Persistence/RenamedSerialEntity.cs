using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_characters.characters")]
internal sealed class RenamedSerialEntity : IMoongateEntity
{
    [Column(Name = "entity_id", OldName = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name", StringLength = 160)]
    public string Name { get; set; } = "";
}
