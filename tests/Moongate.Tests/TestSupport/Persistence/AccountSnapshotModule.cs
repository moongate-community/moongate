using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

public sealed class AccountSnapshotModule : IPersistenceModule
{
    public string Id => "host.accounts";
    public string Schema => "host_accounts";
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Accounts;
    public IReadOnlyCollection<Type> EntityTypes => [typeof(AccountSnapshotEntity)];
}
