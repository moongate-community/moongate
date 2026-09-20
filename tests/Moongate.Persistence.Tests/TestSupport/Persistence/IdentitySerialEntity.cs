using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_invalid.identity_serial_entities")]
internal sealed class IdentitySerialEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, IsIdentity = true)]
    public Serial Id { get; set; }
}
