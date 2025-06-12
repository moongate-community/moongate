using Moongate.Core.Server.Types;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOAccountEntity
{
    public string Id { get; set; }

    public string Username { get; set; }

    public string HashedPassword { get; set; }

    public AccountLevelType AccountLevel { get; set; }

    public DateTime Created { get; set; }

    public DateTime LastLogin { get; set; }

    public bool IsActive { get; set; }
}
