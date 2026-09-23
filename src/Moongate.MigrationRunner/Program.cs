using ConsoleAppFramework;
using Moongate.MigrationRunner.Internal;
using Moongate.Persistence.Migrations.Types.Migrations;

var app = ConsoleApp.Create();
app.Add("status", Cli.StatusAsync);
app.Add("apply", Cli.ApplyAsync);
app.Run(args);

internal static class Cli
{
    /// <summary>Lists migrations pending for the selected target.</summary>
    /// <param name="target">auth (the shared account database) or world (an independent world database).</param>
    /// <param name="rootDirectory">Root directory holding config/moongate.toml. Defaults to MOONGATE_ROOT, then the runner's own parent directory.</param>
    /// <param name="migrationsDirectory">Core migrations directory. Defaults to the configured or bundled migrations directory.</param>
    /// <param name="pluginsDirectory">Plugins directory also scanned for migrations. Defaults to the root directory's plugins subdirectory.</param>
    public static Task<int> StatusAsync(
        MigrationTarget target,
        string? rootDirectory = null,
        string? migrationsDirectory = null,
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default
    )
        => MigrationCommand.StatusAsync(
            target,
            rootDirectory,
            migrationsDirectory,
            pluginsDirectory,
            Console.Out,
            Console.Error,
            cancellationToken
        );

    /// <summary>Applies pending migrations for the selected target.</summary>
    /// <param name="target">auth (the shared account database) or world (an independent world database).</param>
    /// <param name="rootDirectory">Root directory holding config/moongate.toml. Defaults to MOONGATE_ROOT, then the runner's own parent directory.</param>
    /// <param name="migrationsDirectory">Core migrations directory. Defaults to the configured or bundled migrations directory.</param>
    /// <param name="pluginsDirectory">Plugins directory also scanned for migrations. Defaults to the root directory's plugins subdirectory.</param>
    public static Task<int> ApplyAsync(
        MigrationTarget target,
        string? rootDirectory = null,
        string? migrationsDirectory = null,
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default
    )
        => MigrationCommand.ApplyAsync(
            target,
            rootDirectory,
            migrationsDirectory,
            pluginsDirectory,
            Console.Out,
            Console.Error,
            cancellationToken
        );
}
