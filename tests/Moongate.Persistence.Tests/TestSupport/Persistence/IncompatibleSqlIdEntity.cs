using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_invalid.incompatible_entities")]
internal sealed class IncompatibleSqlIdEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, DbType = "numeric")]
    public Serial Id { get; set; }
}
