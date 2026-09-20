using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;
namespace Moongate.Tests.Fixtures.Plugins.PersistencePlugin;

public sealed class PersistencePlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new("fixture.persistenceplugin", "PersistencePlugin", new Version(1, 0));
    public void Register(Container container)
    {
        container.AddPersistenceModule<PluginPersistenceModule>().AddPersistenceEntity<PluginEntity>();
        container.RegisterInstance<Type[]>([typeof(Moongate.Persistence.Services.MoongatePersistenceService), typeof(IFreeSql), typeof(FreeSql.DataAnnotations.TableAttribute), typeof(Npgsql.NpgsqlConnection), typeof(PluginEntity)]);
    }
}
