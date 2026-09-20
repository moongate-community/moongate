using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Services;
namespace Moongate.Tests.TestSupport.Persistence;

public sealed class PersistencePluginLoader : IPluginLoaderService
{
    private readonly Container _container;
    public IReadOnlyList<MoongatePluginData> Plugins => [];
    public int Loads { get; private set; }
    public PersistencePluginLoader(Container container) { _container = container; }
    public void LoadPlugins()
    {
        Loads++;
        _container.AddPersistenceModule<TestPersistenceModule>().AddPersistenceEntity<TestEntity>();
    }
}
