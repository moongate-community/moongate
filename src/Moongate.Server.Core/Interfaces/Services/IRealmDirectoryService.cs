using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns live realm leases and account-visible snapshots.</summary>
public interface IRealmDirectoryService
{
    RealmLease Register(string peerId, RealmRegistration registration, Guid instanceId);

    bool Renew(string peerId, string realmId, Guid leaseId);

    bool Unregister(string peerId, string realmId, Guid leaseId);

    IReadOnlyList<RealmDescriptor> GetAvailable(AccountType accountType);

    void RegisterLocal(RealmDescriptor descriptor);
}
