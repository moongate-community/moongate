using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "a______________________________________________________________.entities")]
internal sealed class MaximumLengthSchemaEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true)]
    public Serial Id { get; set; }
}
