using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_invalid.invalid_key_entities")]
internal sealed class InvalidKeyEntity : IMoongateEntity
{
    [Column(Name = "id")] public Serial Id { get; set; }

    [Column(Name = "code", IsPrimary = true)]
    public int Code { get; set; }
}
