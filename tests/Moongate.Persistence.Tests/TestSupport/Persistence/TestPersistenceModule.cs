using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class TestPersistenceModule : IPersistenceModule
{
    public string Id { get; }

    public string Schema { get; }

    public PersistenceDatabaseTarget DatabaseTarget { get; }

    public IReadOnlyCollection<Type> EntityTypes { get; }

    public TestPersistenceModule(
        string id,
        string schema,
        PersistenceDatabaseTarget databaseTarget,
        IReadOnlyCollection<Type> entityTypes
    )
    {
        Id = id;
        Schema = schema;
        DatabaseTarget = databaseTarget;
        EntityTypes = entityTypes;
    }
}
