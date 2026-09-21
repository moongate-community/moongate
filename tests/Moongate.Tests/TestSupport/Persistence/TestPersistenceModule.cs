using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Tests.TestSupport.Persistence;

public sealed class TestPersistenceModule : IPersistenceModule
{
    public string Id => "host.test";
    public string Schema => "host_test";
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Realm;
    public IReadOnlyCollection<Type> EntityTypes => [typeof(TestEntity)];
}
