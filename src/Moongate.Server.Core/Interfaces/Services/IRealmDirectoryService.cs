using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Realms;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Owns live realm leases and account-visible snapshots.</summary>
public interface IRealmDirectoryService
{
    /// <summary>Registers or refreshes a remote realm for its authenticated peer.</summary>
    /// <param name="peerId">The authenticated peer identity, which must match the realm ID.</param>
    /// <param name="registration">The realm address and visibility settings.</param>
    /// <param name="instanceId">The identity of this realm process instance.</param>
    /// <returns>The lease used for later renewal and removal.</returns>
    RealmLease Register(string peerId, RealmRegistration registration, Guid instanceId);

    /// <summary>Renews a remote realm lease while its peer and lease identities remain current.</summary>
    /// <param name="peerId">The authenticated peer identity.</param>
    /// <param name="realmId">The realm to renew.</param>
    /// <param name="leaseId">The lease returned by registration.</param>
    /// <returns><see cref="RealmRegistrationError.None" /> on success; otherwise the reason renewal was rejected.</returns>
    RealmRegistrationError Renew(string peerId, string realmId, Guid leaseId);

    /// <summary>Removes a remote realm only when the peer and lease still match.</summary>
    /// <param name="peerId">The authenticated peer identity.</param>
    /// <param name="realmId">The realm to remove.</param>
    /// <param name="leaseId">The current lease identifier.</param>
    /// <returns><see langword="true" /> when the realm was removed.</returns>
    bool Unregister(string peerId, string realmId, Guid leaseId);

    /// <summary>Gets unexpired local and remote realms visible to the given account type, ordered by server index.</summary>
    /// <param name="accountType">The account type used to filter realm access.</param>
    /// <returns>A snapshot of the currently available realms.</returns>
    IReadOnlyList<RealmDescriptor> GetAvailable(AccountType accountType);

    /// <summary>Adds a realm hosted in this process to the directory.</summary>
    /// <param name="descriptor">The local realm and its client-facing address.</param>
    void RegisterLocal(RealmDescriptor descriptor);
}
