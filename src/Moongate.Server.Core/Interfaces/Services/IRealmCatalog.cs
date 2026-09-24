using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Interfaces.Services;

/// <summary>Reads live account-visible realms for login and redirect packets.</summary>
public interface IRealmCatalog
{
    /// <summary>Returns the currently available realms visible to the account level.</summary>
    ValueTask<IReadOnlyList<RealmDescriptor>> GetAvailableAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default
    );

    /// <summary>Finds the selected live realm and its owning process generation.</summary>
    ValueTask<RealmInstance?> FindByIndexAsync(
        ushort index,
        AccountType accountType,
        CancellationToken cancellationToken = default
    );
}
