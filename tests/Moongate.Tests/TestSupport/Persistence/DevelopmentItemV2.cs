using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Tests.TestSupport.Persistence;

[Table(Name = "host_test.items")]
public sealed class DevelopmentItemV2 : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    public string Name { get; set; } = "";

    public int? Level { get; set; }
}
