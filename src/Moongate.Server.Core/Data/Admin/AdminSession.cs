
namespace Moongate.Server.Core.Data.Admin;

/// <summary>Immutable administration session snapshot.</summary>
public sealed class AdminSession
{
    public AdminIdentity Identity { get; }
    public Guid Generation { get; }
    public DateTimeOffset ExpiresAt { get; }

    public AdminSession(AdminIdentity identity, Guid generation, DateTimeOffset expiresAt)
    {
        Identity = identity;
        Generation = generation;
        ExpiresAt = expiresAt;
    }
}
