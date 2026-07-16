using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Interfaces;

namespace Moongate.Persistence.Entities;

public class AccountEntity : ISerialIdEntity
{
    public Serial Id { get; set; }

    public string Username { get; set; }

    public string? Email { get; set; }

    public string PasswordHash { get; set; }

    public bool IsActive { get; set; }

    public string ActivationToken { get; set; }

    public AccountLevelType AccountLevel { get; set; }

    public List<Serial> MobileIds { get; set; } = new();
}
