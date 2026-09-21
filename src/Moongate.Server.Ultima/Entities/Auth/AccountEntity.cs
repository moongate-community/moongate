using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Entities.Auth;

[Table(Name = "auth.accounts")]
public class AccountEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    public string Username { get; set; }

    public string HashPassword { get; set; }

    public AccountType AccountType { get; set; }

    public string Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsLocked { get; set; }
}
