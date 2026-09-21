using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.development_rename")]
internal sealed class DevelopmentRenamedEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(OldName = "old_level")] public int? Level { get; set; }
}
