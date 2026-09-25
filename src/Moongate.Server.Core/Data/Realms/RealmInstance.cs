namespace Moongate.Server.Core.Data.Realms;

/// <summary>
///     A live realm descriptor and the process generation that owns its lease.
/// </summary>
public sealed record RealmInstance
{
    public RealmDescriptor Descriptor { get; }

    public Guid InstanceId { get; }

    public RealmInstance(RealmDescriptor descriptor, Guid instanceId)
    {
        Descriptor = descriptor;
        InstanceId = instanceId;
    }
}
