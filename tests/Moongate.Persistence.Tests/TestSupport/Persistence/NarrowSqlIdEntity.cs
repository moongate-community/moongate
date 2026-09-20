using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_invalid.narrow_entities")]
internal sealed class NarrowSqlIdEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, DbType = "integer")]
    public Serial Id { get; set; }
}
