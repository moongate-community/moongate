using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.development_indexed_accounts"),
 Index("ux_development_indexed_username", nameof(Username), true),
 Index("ix_development_indexed_lookup", "Username,Id DESC", false),
 Index("ix_development_indexed_id", nameof(Id), false)]
internal sealed class DevelopmentIndexedAccountEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(StringLength = 255, IsNullable = false)]
    public string Username { get; set; } = string.Empty;
}
