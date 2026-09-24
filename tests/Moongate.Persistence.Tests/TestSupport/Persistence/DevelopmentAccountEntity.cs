using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

[Table(Name = "auth.development_accounts")]
internal sealed class DevelopmentAccountEntity : IMoongateEntity
{
    [Column(IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(StringLength = 255)]
    public string? Username { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
