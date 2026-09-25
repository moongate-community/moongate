using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Tests.TestSupport.Persistence.Data;

namespace Moongate.Tests.TestSupport.Persistence;

[Table(Name = "host_test.items")]
public sealed class DevelopmentInitializedJsonEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    public string Name { get; set; } = "";

    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public List<DevelopmentJsonValue>? Progress { get; set; } = [];
}
