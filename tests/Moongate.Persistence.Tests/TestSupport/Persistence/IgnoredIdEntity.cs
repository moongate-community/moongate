using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "plugin_invalid.ignored_id_entities")]
internal sealed class IgnoredIdEntity : IMoongateEntity
{
    [Column(Name = "id", IsIgnore = true)] public Serial Id { get; set; }

    [Column(Name = "code", IsPrimary = true)]
    public int Code { get; set; }
}
