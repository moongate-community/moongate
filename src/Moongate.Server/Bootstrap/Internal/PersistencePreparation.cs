using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config;
using Serilog;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     Loads the host's plugin registrations before an explicit persistence phase.
/// </summary>
internal static class PersistencePreparation
{
    public static async Task DisposePersistenceAsync(Container container)
    {
        if (container.IsRegistered<MoongatePersistenceService>())
        {
            await container.Resolve<MoongatePersistenceService>().DisposeAsync();
        }
    }

    public static async Task<bool> InitializeAsync(Container container, CancellationToken cancellationToken)
    {
        LoadPlugins(container);

        if (container.IsRegistered<MoongatePersistenceService>())
        {
            var autoSync = container.IsRegistered<MoongateServerConfig>() &&
                           container.Resolve<MoongateServerConfig>().Persistence.AutoSyncSchema;
            var autoGenerate = container.IsRegistered<MoongateServerConfig>() &&
                               container.Resolve<MoongateServerConfig>().Persistence.AutoGenerateMigrations;
            var autoApply = container.IsRegistered<MoongateServerConfig>() &&
                            container.Resolve<MoongateServerConfig>().Persistence.AutoApplyMigrations;
            Log.Information(
                "Preparing PostgreSQL persistence; schema mode {SchemaMode}",
                autoGenerate ? "generate-migrations" :
                autoSync ? "synchronize" :
                autoApply ? "apply-migrations" : "validate"
            );

            if (autoApply)
            {
                await StartupMigrations.PrepareAsync(
                    container.Resolve<MoongateServerConfig>(),
                    container.Resolve<DirectoriesConfig>(),
                    Path.Combine(AppContext.BaseDirectory, "migrations"),
                    (root, migrations, plugins) => new DevelopmentMigrationRunner(root, migrations, plugins),
                    cancellationToken
                );
            }

            try
            {
                await container.Resolve<MoongatePersistenceService>()
                    .InitializeAsync(cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith(
                                                                  "PostgreSQL schema changes are required",
                                                                  StringComparison.Ordinal
                                                              ))
            {
                throw new InvalidOperationException(
                    exception.Message +
                    " Use --persistence-schema generate to write a draft, review it, then run mgctl migrate apply.",
                    exception
                );
            }

            return true;
        }

        return false;
    }

    public static void LoadPlugins(Container container)
    {
        if (container.IsRegistered<IPluginLoaderService>())
        {
            container.Resolve<IPluginLoaderService>().LoadPlugins();
        }
    }
}
