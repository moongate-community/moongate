using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Tests.TestSupport.Persistence;

[Table(Name = "host_accounts.items")]
public sealed class AccountSnapshotEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(Name = "name")]
    public string Name { get; set; } = "";
}
