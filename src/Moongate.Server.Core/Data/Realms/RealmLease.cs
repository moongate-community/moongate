namespace Moongate.Server.Core.Data.Realms;

/// <summary>Opaque generation accepted for renewal and unregister.</summary>
public sealed class RealmLease
{
    public Guid LeaseId { get; }

    public RealmLease(Guid leaseId)
    {
        LeaseId = leaseId;
    }
}
