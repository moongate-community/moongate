using Moongate.Generators.Annotations.Persistence;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Represents a persisted account with character ownership metadata.
/// </summary>
public partial class UOAccountEntity
{
    [MoongatePersistedMember(0)]
    public Serial Id { get; set; }

    [MoongatePersistedMember(1)]
    public string Username { get; set; }

    [MoongatePersistedMember(2)]
    public string PasswordHash { get; set; }

    [MoongatePersistedMember(3)]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [MoongatePersistedMember(4)]
    public DateTime LastLoginUtc { get; set; } = DateTime.UtcNow;

    [MoongatePersistedMember(5)]
    public List<Serial> CharacterIds { get; set; } = [];

    [MoongatePersistedMember(6)]
    public AccountType AccountType { get; set; } = AccountType.Regular;

    [MoongatePersistedMember(7)]
    public string Email { get; set; }

    [MoongatePersistedMember(8)]
    public bool IsLocked { get; set; }

    [MoongatePersistedMember(9)]
    public string? ActivationId { get; set; }

    [MoongatePersistedMember(10)]
    public string? RecoveryCode { get; set; }
}
