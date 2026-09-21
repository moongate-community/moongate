using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.development_defaults")]
internal sealed class DevelopmentDefaultEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(IsNullable = false, DbType = "int4 NOT NULL DEFAULT 7")]
    public int Level { get; set; } = 7;
}
