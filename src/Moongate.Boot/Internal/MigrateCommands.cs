using Moongate.MigrationRunner.Internal;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Boot.Internal;

/// <summary>
///     The <c>mgboot migrate</c> commands: the versioned SQL migrations of the auth and world databases.
/// </summary>
internal static class MigrateCommands
{
    /// <summary>
    ///     Lists migrations pending for the selected target.
    /// </summary>
    /// <param name="target">
    ///     auth (the shared account database) or world (an independent world database).
    /// </param>
    /// <param name="rootDirectory">
    ///     Root directory holding config/moongate.toml. Defaults to MOONGATE_ROOT, then mgboot's own
    ///     directory.
    /// </param>
    /// <param name="migrationsDirectory">
    ///     Core migrations directory. Defaults to the configured or bundled migrations directory.
    /// </param>
    /// <param name="pluginsDirectory">
    ///     Plugins directory also scanned for migrations. Defaults to the root directory's plugins
    ///     subdirectory.
    /// </param>
    public static Task<int> StatusAsync(
        MigrationTarget target,
        string? rootDirectory = null,
        string? migrationsDirectory = null,
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        return MigrationCommand.StatusAsync(
            target,
            rootDirectory,
            migrationsDirectory,
            pluginsDirectory,
            Console.Out,
            Console.Error,
            cancellationToken
        );
    }

    /// <summary>
    ///     Applies pending migrations for the selected target.
    /// </summary>
    /// <param name="target">
    ///     auth (the shared account database) or world (an independent world database).
    /// </param>
    /// <param name="rootDirectory">
    ///     Root directory holding config/moongate.toml. Defaults to MOONGATE_ROOT, then mgboot's own
    ///     directory.
    /// </param>
    /// <param name="migrationsDirectory">
    ///     Core migrations directory. Defaults to the configured or bundled migrations directory.
    /// </param>
    /// <param name="pluginsDirectory">
    ///     Plugins directory also scanned for migrations. Defaults to the root directory's plugins
    ///     subdirectory.
    /// </param>
    public static Task<int> ApplyAsync(
        MigrationTarget target,
        string? rootDirectory = null,
        string? migrationsDirectory = null,
        string? pluginsDirectory = null,
        CancellationToken cancellationToken = default
    )
    {
        return MigrationCommand.ApplyAsync(
            target,
            rootDirectory,
            migrationsDirectory,
            pluginsDirectory,
            Console.Out,
            Console.Error,
            cancellationToken
        );
    }
}
