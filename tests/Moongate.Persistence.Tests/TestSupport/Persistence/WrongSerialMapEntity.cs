using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_invalid.wrong_map_entities")]
internal sealed class WrongSerialMapEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(int))]
    public Serial Id { get; set; }
}
