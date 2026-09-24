using Moongate.Core.Extensions.Directories;
using Moongate.Core.Extensions.Env;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>Configures lazy PostgreSQL target connections and explicit schema synchronization.</summary>
public sealed class PersistenceConfig
{
    public bool AutoSyncSchema { get; set; }

    public bool AutoGenerateMigrations { get; set; }

    public string? MigrationsDirectory { get; set; }

    public PersistenceDatabaseConfig Accounts { get; set; } =
        new() { ConnectionString = "postgres://moongate:moongate@localhost:5432/auth" };

    public PersistenceDatabaseConfig Realm { get; set; } =
        new() { ConnectionString = "postgres://moongate:moongate@localhost:5432/world" };

    public PostgreSqlPersistenceOptions ToOptions(
        string? migrationsDirectory = null,
        string? pluginsDirectory = null,
        ServerMode mode = ServerMode.Standalone,
        string? rootDirectory = null
    )
    {
        Validate(mode);
        migrationsDirectory = ResolveMigrationsDirectory(migrationsDirectory);

        var development = AutoGenerateMigrations
                              ? new DevelopmentMigrationOptions(
                                  migrationsDirectory!,
                                  pluginsDirectory,
                                  new DevelopmentMigrationRunner(
                                      rootDirectory ??
                                      Environment.GetEnvironmentVariable("MOONGATE_ROOT") ?? AppContext.BaseDirectory,
                                      migrationsDirectory!,
                                      pluginsDirectory
                                  ),
                                  MigrationComponentResolver.Resolve
                              )
                              : null;

        var databases = mode switch
        {
            ServerMode.Login => [Accounts.ToOptions(PersistenceDatabaseTarget.Accounts)],
            ServerMode.Game  => [Realm.ToOptions(PersistenceDatabaseTarget.Realm)],
            ServerMode.Standalone => new[]
            {
                Accounts.ToOptions(PersistenceDatabaseTarget.Accounts),
                Realm.ToOptions(PersistenceDatabaseTarget.Realm)
            },
            _ => throw new InvalidOperationException("Unsupported server mode.")
        };

        return new(
            databases,
            AutoSyncSchema,
            migrationsDirectory is null
                ? null
                : target => MigrationCatalog.Load(
                      migrationsDirectory,
                      pluginsDirectory,
                      target == PersistenceDatabaseTarget.Accounts ? MigrationTarget.Auth : MigrationTarget.World
                  ),
            target => (mode & (target == PersistenceDatabaseTarget.Accounts ? ServerMode.Login : ServerMode.Game)) != 0,
            development
        );
    }

    public string? ResolveMigrationsDirectory(string? fallback = null)
        => string.IsNullOrWhiteSpace(MigrationsDirectory)
               ? fallback
               : MigrationsDirectory.ExpandEnvironmentVariables(true).ResolvePathAndEnvs();

    public void Validate()
        => Validate(ServerMode.Standalone);

    public void Validate(ServerMode mode)
    {
        if (AutoGenerateMigrations && AutoSyncSchema)
        {
            throw new InvalidOperationException("auto_generate_migrations and auto_sync_schema are mutually exclusive.");
        }

        if (AutoGenerateMigrations && string.IsNullOrWhiteSpace(MigrationsDirectory))
        {
            throw new InvalidOperationException(
                "auto_generate_migrations requires an explicit source migrations_directory."
            );
        }

        if (mode is not (ServerMode.Login or ServerMode.Game or ServerMode.Standalone))
        {
            throw new InvalidOperationException("Unsupported server mode.");
        }

        if ((mode & ServerMode.Login) != 0 && Accounts is null ||
            (mode & ServerMode.Game) != 0 && Realm is null)
        {
            throw new InvalidOperationException("The active persistence configuration section cannot be null.");
        }

        if ((mode & ServerMode.Login) != 0)
        {
            Accounts!.Validate();
        }

        if ((mode & ServerMode.Game) != 0)
        {
            Realm!.Validate();
        }
    }
}
