using Moongate.Server.Core.Data.Realms;

namespace Moongate.Server.Services.Realms.Internal;

internal sealed class RealmDirectoryEntry
{
    public string PeerId { get; }

    public Guid InstanceId { get; }

    public Guid LeaseId { get; }

    public RealmDescriptor Descriptor { get; }

    public long LastRenewedTimestamp { get; set; }

    public RealmDirectoryEntry(
        string peerId,
        Guid instanceId,
        Guid leaseId,
        RealmDescriptor descriptor,
        long lastRenewedTimestamp
    )
    {
        PeerId = peerId;
        InstanceId = instanceId;
        LeaseId = leaseId;
        Descriptor = descriptor;
        LastRenewedTimestamp = lastRenewedTimestamp;
    }
}
