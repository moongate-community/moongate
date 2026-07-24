using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Persistence.Generators;
using Serilog;
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
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.persistence.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongatePersistencePlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Persistence",
            Description = "Moongate persistence plugin"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        var persistenceConfig = new PersistenceConfig();

        var directoriesConfig = container.Resolve<DirectoriesConfig>();

        directoriesConfig.RegisterDirectory("saves");

        persistenceConfig.SaveDirectory = directoriesConfig.GetPath("saves");

        container.RegisterMessagePackSerializer();
        container.RegisterPersistence(persistenceConfig);

        // No type ids here: SquidStd derives one from the store name, so nothing has to know which are
        // taken and a plugin cannot collide with the host.
        container.RegisterPersistedEntity<AccountEntity, Serial>(
            "accounts",
            1,
            entity => entity.Id,
            (entity, id) => entity.Id = id,
            new DefaultSerialGenerator()
        );

        container.RegisterPersistedEntity<MobileEntity, Serial>(
            "mobiles",
            1,
            entity => entity.Id,
            (entity, id) => entity.Id = id,
            new MobileSerialGenerator()
        );
        container.RegisterPersistedEntity<ItemEntity, Serial>(
            "items",
            1,
            entity => entity.Id,
            (entity, id) => entity.Id = id,
            new ItemSerialGenerator()
        );
        container.RegisterPersistedEntity<ServerSettingsEntity, Serial>(
            "server_settings",
            1,
            entity => entity.Id,
            (entity, id) => entity.Id = id,
            new DefaultSerialGenerator()
        );
        container.RegisterPersistenceSeeder(async (service, token) =>
            {
                var accountStore = service.GetStore<AccountEntity, Serial>();

                var accountEntity = new AccountEntity
                {
                    Username = "admin",
                    PasswordHash = HashUtils.HashPassword("admin"),
                    IsActive = true,
                    AccountLevel = AccountLevelType.Administrator
                };

                await accountStore.UpsertAsync(accountEntity, token);

                Log.Logger.Warning("!!!! Default account created!!!");
                Log.Logger.Warning("!!!! Username: admin, Password: admin !!!!");

                await service.SaveSnapshotAsync(token);
            }
        );
    }
}
