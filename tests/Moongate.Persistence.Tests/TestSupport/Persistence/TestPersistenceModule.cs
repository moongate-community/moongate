using FreeSql;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal sealed class TestPersistenceModule : IPersistenceModule
{
    private readonly Action<IFreeSql> _configure;

    public string Id { get; }

    public string Schema { get; }

    public PersistenceDatabaseTarget DatabaseTarget { get; }

    public IReadOnlyCollection<Type> EntityTypes { get; }

    public TestPersistenceModule(
        string id,
        string schema,
        PersistenceDatabaseTarget databaseTarget,
        IReadOnlyCollection<Type> entityTypes,
        Action<IFreeSql> configure
    )
    {
        Id = id;
        Schema = schema;
        DatabaseTarget = databaseTarget;
        EntityTypes = entityTypes;
        _configure = configure;
    }

    public void Configure(IFreeSql orm)
    {
        _configure(orm);
    }
}
