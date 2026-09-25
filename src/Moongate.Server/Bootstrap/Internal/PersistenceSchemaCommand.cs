using System.Text;
using System.Text.RegularExpressions;
using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Helpers;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Types.Persistence;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     Composes only registration prerequisites and executes schema administration without host startup.
/// </summary>
internal static class PersistenceSchemaCommand
{
    public static async Task ExecuteAsync(
        string rootDirectory,
        PersistenceSchemaMode mode,
        TextWriter output,
        CancellationToken cancellationToken,
        string? migrationOutput = null,
        string? migrationTarget = null
    )
    {
        using var container = new Container();
        var directories = new DirectoriesConfig(rootDirectory, ["config", "plugins"]);
        var config = ConfigHelper.Load(Path.Combine(directories["config"], "moongate.toml"));
        container.RegisterInstance(directories);
        container.RegisterInstance(config);
        container.RegisterMoongateEventBus();
        container.RegisterMoongatePersistence(
            config.Persistence.ToOptions(pluginsDirectory: directories["plugins"], rootDirectory: rootDirectory)
        );
        container.RegisterInstance<IPluginLoaderService>(new PluginLoaderService(container, directories));
        await using var persistence = container.Resolve<MoongatePersistenceService>();
        await RunAsync(container, mode, output, cancellationToken, migrationOutput, migrationTarget).ConfigureAwait(false);
    }

    public static async Task RunAsync(
        Container container,
        PersistenceSchemaMode mode,
        TextWriter output,
        CancellationToken cancellationToken,
        string? migrationOutput = null,
        string? migrationTarget = null
    )
    {
        if (mode == PersistenceSchemaMode.Apply)
        {
            throw new InvalidOperationException(
                "Direct schema apply has been replaced. Review versioned SQL and run Moongate.MigrationRunner apply --target auth|world."
            );
        }

        if (mode is not (PersistenceSchemaMode.Preview or PersistenceSchemaMode.Generate))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Choose --persistence-schema preview or generate.");
        }

        PersistenceDatabaseTarget? selectedTarget = null;

        if (mode == PersistenceSchemaMode.Generate)
        {
            selectedTarget = migrationTarget switch
            {
                "auth"  => PersistenceDatabaseTarget.Accounts,
                "world" => PersistenceDatabaseTarget.Realm,
                _       => throw new InvalidOperationException("Generate requires --migration-target auth|world.")
            };

            if (migrationOutput is null ||
                !Regex.IsMatch(
                    Path.GetFileName(migrationOutput),
                    @"^[0-9]{4}_[a-z][a-z0-9_]*\.sql$",
                    RegexOptions.CultureInvariant
                ) ||
                Path.GetFileName(migrationOutput).StartsWith("0000_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generate requires --migration-output PATH/NNNN_description.sql with a positive sequence."
                );
            }

            if (File.Exists(migrationOutput))
            {
                throw new IOException("The migration output already exists. Existing migrations cannot be overwritten.");
            }
        }

        PersistencePreparation.LoadPlugins(container);
        var persistence = container.Resolve<MoongatePersistenceService>();
        var changes = await persistence.PreviewSchemaAsync(cancellationToken).ConfigureAwait(false);

        if (mode == PersistenceSchemaMode.Generate)
        {
            var selected = changes.Where(change => change.Target == selectedTarget).ToArray();

            if (selected.Length == 0)
            {
                throw new InvalidOperationException(
                    "No schema changes found for the selected target; no migration was written."
                );
            }

            var sql = "-- Draft generated against the current reference database. Review before applying.\n" +
                      string.Join("\n", selected.Select(change => $"-- {change.ModuleId}\n{change.Ddl}"));
            var path = Path.GetFullPath(migrationOutput!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + $".{Guid.NewGuid():N}.tmp";

            try
            {
                await File.WriteAllTextAsync(temporary, sql, new UTF8Encoding(false), cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, path, false);
            }
            finally
            {
                File.Delete(temporary);
            }

            await output.WriteLineAsync(
                $"Draft written to {path}. Review and commit it before running Moongate.MigrationRunner apply."
            );

            return;
        }

        foreach (var change in changes)
        {
            await output.WriteLineAsync($"-- {change.Target}: {change.ModuleId}");
            await output.WriteLineAsync(change.Ddl);
        }

        if (changes.Count == 0)
        {
            await output.WriteLineAsync("No PostgreSQL schema changes required.");
        }
    }
}
