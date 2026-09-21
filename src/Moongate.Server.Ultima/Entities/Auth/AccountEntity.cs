using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Ultima.Entities.Auth;

[Table(Name = "auth.accounts"), Index("ux_accounts_username", nameof(Username), true)]
public class AccountEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    [Column(IsNullable = false)] public string Username { get; set; } = string.Empty;

    [Column(IsNullable = false)] public string HashPassword { get; set; } = string.Empty;

    public AccountType AccountType { get; set; }

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsLocked { get; set; }
}
