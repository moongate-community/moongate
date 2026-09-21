using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Helpers;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Types.Persistence;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>Composes only registration prerequisites and executes schema administration without host startup.</summary>
internal static class PersistenceSchemaCommand
{
    public static async Task ExecuteAsync(
        string rootDirectory, PersistenceSchemaMode mode, TextWriter output,
        CancellationToken cancellationToken
    )
    {
        using var container = new Container();
        var directories = new DirectoriesConfig(rootDirectory, ["config", "plugins"]);
        var config = ConfigHelper.Load(Path.Combine(directories["config"], "moongate.toml"));
        container.RegisterInstance(directories);
        container.RegisterInstance(config);
        container.RegisterMoongateEventBus();
        container.RegisterMoongatePersistence(config.Persistence.ToOptions());
        container.RegisterInstance<IPluginLoaderService>(new PluginLoaderService(container, directories));
        await using var persistence = container.Resolve<MoongatePersistenceService>();
        await RunAsync(container, mode, output, cancellationToken).ConfigureAwait(false);
    }

    public static async Task RunAsync(
        Container container, PersistenceSchemaMode mode, TextWriter output,
        CancellationToken cancellationToken
    )
    {
        if (mode is not (PersistenceSchemaMode.Preview or PersistenceSchemaMode.Apply))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Choose --persistence-schema preview or apply.");
        }

        PersistencePreparation.LoadPlugins(container);
        var persistence = container.Resolve<MoongatePersistenceService>();
        var changes = await persistence.PreviewSchemaAsync(cancellationToken).ConfigureAwait(false);
        foreach (var change in changes)
        {
            await output.WriteLineAsync($"-- {change.Target}: {change.ModuleId}");
            await output.WriteLineAsync(change.Ddl);
        }

        if (mode == PersistenceSchemaMode.Apply)
        {
            await persistence.SynchronizeSchemaAsync(cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync("PostgreSQL schema synchronization completed.");
        }
        else if (changes.Count == 0)
        {
            await output.WriteLineAsync("No PostgreSQL schema changes required.");
        }
    }
}
