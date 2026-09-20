using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;
namespace Moongate.Tests.Fixtures.Plugins.CollisionPlugin;

public sealed class CollisionPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new("fixture.collisionplugin", "CollisionPlugin", new Version(1, 0));
    public void Register(Container container)
    {
        container.AddPersistenceModule<PluginPersistenceModule>().AddPersistenceEntity<PluginEntity>();

    }
}
