using MemoryPack;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

/// <summary>
/// Represents a persisted account with character ownership metadata.
/// </summary>
[MemoryPackable(SerializeLayout.Explicit)]
public partial class UOAccountEntity
{
    [MemoryPackOrder(0)]
    public Serial Id { get; set; }

    [MemoryPackOrder(1)]
    public string Username { get; set; }

    [MemoryPackOrder(2)]
    public string PasswordHash { get; set; }

    [MemoryPackOrder(3)]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(4)]
    public DateTime LastLoginUtc { get; set; } = DateTime.UtcNow;

    [MemoryPackOrder(5)]
    public List<Serial> CharacterIds { get; set; } = [];

    [MemoryPackOrder(6)]
    public AccountType AccountType { get; set; } = AccountType.Regular;

    [MemoryPackOrder(7)]
    public string Email { get; set; }

    [MemoryPackOrder(8)]
    public bool IsLocked { get; set; }

    [MemoryPackOrder(9)]
    public string? ActivationId { get; set; }

    [MemoryPackOrder(10)]
    public string? RecoveryCode { get; set; }
}
