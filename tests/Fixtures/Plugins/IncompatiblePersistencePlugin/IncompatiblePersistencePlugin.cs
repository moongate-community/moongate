using DryIoc;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;
namespace Moongate.Tests.Fixtures.Plugins.IncompatiblePersistencePlugin;

public sealed class IncompatiblePersistencePlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new("fixture.incompatible", "Incompatible", new Version(1, 0));
    public void Register(Container container) { container.RegisterInstance(typeof(MoongatePersistenceService)); }
}
