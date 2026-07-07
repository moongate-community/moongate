using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Persistence.Abstractions.Data;
using SquidStd.Persistence.Extensions;
using SquidStd.Persistence.MessagePack.Extensions;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Persistence;

public class MoongatePersistencePlugin : ISquidStdPlugin
{
    public void Configure(IContainer container, PluginContext context)
    {
        var persistenceConfig = new PersistenceConfig();
        
        var directoriesConfig = container.Resolve<DirectoriesConfig>();

        directoriesConfig.RegisterDirectory("saves");

        persistenceConfig.SaveDirectory = directoriesConfig.GetPath("saves");

        container.RegisterMessagePackSerializer();
        container.RegisterPersistence(persistenceConfig);

        container.RegisterPersistedEntity<AccountEntity, Serial>(1, "accounts", 1, entity => entity.Id);
    }

    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.persistence.plugin",
            Version = new Version(VersionUtils.GetVersion(typeof(MoongatePersistencePlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Persistence",
            Description = "Moongate persistence plugin",
        };
}
