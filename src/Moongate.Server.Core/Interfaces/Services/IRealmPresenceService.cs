using Moongate.Server.Core.Data.Realms;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Publishes and renews one process-owned realm lease.</summary>
public interface IRealmPresenceService
{
    /// <summary>Claims or replaces a lease for the realm's server index.</summary>
    ValueTask RegisterAsync(RealmInstance realm, CancellationToken cancellationToken = default);

    /// <summary>Atomically renews an owned lease or restores a missing one; false means another process owns it.</summary>
    ValueTask<bool> RenewAsync(RealmInstance realm, CancellationToken cancellationToken = default);

    /// <summary>Removes the lease only if the process still owns it.</summary>
    ValueTask UnregisterAsync(RealmInstance realm, CancellationToken cancellationToken = default);
}
