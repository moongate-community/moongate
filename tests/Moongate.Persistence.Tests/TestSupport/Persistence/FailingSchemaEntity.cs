using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_failing.entities")]
internal sealed class FailingSchemaEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }

    [Column(Name = "value", DbType = "type_that_does_not_exist")]
    public string Value { get; set; } = "";
}
