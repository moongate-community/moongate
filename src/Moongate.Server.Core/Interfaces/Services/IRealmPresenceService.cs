using Moongate.Server.Core.Data.Realms;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Publishes and renews one process-owned realm lease.</summary>
public interface IRealmPresenceService
{
    /// <summary>Claims or replaces a lease for the realm's server index.</summary>
    ValueTask RegisterAsync(RealmInstance realm, CancellationToken cancellationToken = default);

    /// <summary>Restores a missing lease without replacing a different process generation.</summary>
    ValueTask<bool> TryRestoreAsync(RealmInstance realm, CancellationToken cancellationToken = default);

    /// <summary>Renews only if the process still owns the lease.</summary>
    ValueTask<bool> RenewAsync(RealmInstance realm, CancellationToken cancellationToken = default);

    /// <summary>Removes the lease only if the process still owns it.</summary>
    ValueTask UnregisterAsync(RealmInstance realm, CancellationToken cancellationToken = default);
}
