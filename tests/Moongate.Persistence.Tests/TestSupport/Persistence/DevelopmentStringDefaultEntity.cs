using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.development_string_defaults")]
internal sealed class DevelopmentStringDefaultEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(DbType = "varchar(255) DEFAULT 'guest'", IsNullable = true)]
    public string? Name { get; set; }
}
