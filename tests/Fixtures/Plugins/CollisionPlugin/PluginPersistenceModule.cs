using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
namespace Moongate.Tests.Fixtures.Plugins.CollisionPlugin;

public sealed class PluginPersistenceModule : IPersistenceModule
{
    public string Id => "fixture.collisionplugin";
    public string Schema => "fixture_data";
    public PersistenceDatabaseTarget DatabaseTarget => PersistenceDatabaseTarget.Realm;
    public IReadOnlyCollection<Type> EntityTypes => [typeof(PluginEntity)];
}
