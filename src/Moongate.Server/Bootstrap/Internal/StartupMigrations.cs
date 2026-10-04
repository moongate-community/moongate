using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Bootstrap.Internal.Setup;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config.Sections;
using Serilog;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     What <c>persistence.auto_apply_migrations</c> does before the persistence check: it adds to the migrations
///     directory the bundled core SQL it lacks, then applies the pending reviewed SQL of the targets this process
///     uses, through the migration runner. It never generates SQL; with <c>auto_generate_migrations</c> also on, the
///     development start applies, so only the copy runs here.
/// </summary>
internal static class StartupMigrations
{
    public static async Task PrepareAsync(
        PersistenceConfig config,
        string bundledDirectory,
        string migrationsDirectory,
        ServerMode mode,
        IDevelopmentMigrationRunner runner,
        CancellationToken cancellationToken
    )
    {
        if (!config.AutoApplyMigrations)
        {
            return;
        }

        foreach (var created in BundledMigrations.CopyMissing(bundledDirectory, migrationsDirectory))
        {
            Log.Information("Added the bundled migration {Migration}", created);
        }

        if (config.AutoGenerateMigrations)
        {
            return;
        }

        runner.ValidateAvailable();

        foreach (var (role, target) in new[]
                 {
                     (ServerMode.Login, PersistenceDatabaseTarget.Accounts),
                     (ServerMode.Game, PersistenceDatabaseTarget.Realm)
                 })
        {
            if ((mode & role) != 0)
            {
                await runner.ApplyAsync(target, cancellationToken);
            }
        }
    }
}
