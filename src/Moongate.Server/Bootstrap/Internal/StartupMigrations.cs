using Moongate.Core.Directories;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Serilog;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     What <c>persistence.auto_apply_migrations</c> does before the persistence check: it adds to the migrations
///     directory the bundled core SQL it lacks, then applies the pending reviewed SQL of the targets this process
///     uses, and of the plugin bundles in its plugins directory, through the migration runner. It never generates SQL;
///     with <c>auto_generate_migrations</c> also on, the
///     development start applies, so only the copy runs here.
/// </summary>
internal static class StartupMigrations
{
    public static async Task PrepareAsync(
        MoongateServerConfig config,
        DirectoriesConfig directories,
        string bundledDirectory,
        Func<string, string, string?, IDevelopmentMigrationRunner> createRunner,
        CancellationToken cancellationToken
    )
    {
        var persistence = config.Persistence;

        if (!persistence.AutoApplyMigrations)
        {
            return;
        }

        // The directory the persistence options read, and the plugins whose SQL the runner applies with the core's.
        var migrationsDirectory = persistence.ResolveMigrationsDirectory(Path.Combine(directories.Root, "migrations"))!;

        foreach (var created in BundledMigrations.CopyMissing(bundledDirectory, migrationsDirectory))
        {
            Log.Information("Added the bundled migration {Migration}", created);
        }

        if (persistence.AutoGenerateMigrations)
        {
            return;
        }

        var runner = createRunner(directories.Root, migrationsDirectory, directories["plugins"]);
        runner.ValidateAvailable();

        foreach (var (role, target) in new[]
                 {
                     (ServerMode.Login, PersistenceDatabaseTarget.Accounts),
                     (ServerMode.Game, PersistenceDatabaseTarget.Realm)
                 })
        {
            if ((config.Mode & role) != 0)
            {
                await runner.ApplyAsync(target, cancellationToken);
            }
        }
    }
}
