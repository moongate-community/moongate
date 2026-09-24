using Moongate.Core.Extensions.Directories;
using Moongate.Core.Extensions.Env;
using Moongate.Core.Utils;
using Moongate.MigrationRunner.Data.Internal;
using Moongate.MigrationRunner.Services;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Npgsql;

namespace Moongate.MigrationRunner.Internal;

/// <summary>
/// The real logic, testable in-process: no CLI parsing (Program.cs's Cli class and
/// ConsoleAppFramework own that), output written to the given writers rather than
/// <see cref="Console" /> directly, and the exit code returned rather than set on
/// <see cref="Environment.ExitCode" />.
/// </summary>
internal static class MigrationCommand
{
    public static Task<int> StatusAsync(
        MigrationTarget target,
        string? rootDirectory,
        string? migrationsDirectory,
        string? pluginsDirectory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default
    )
        => RunAsync(false, target, rootDirectory, migrationsDirectory, pluginsDirectory, output, error, cancellationToken);

    public static Task<int> ApplyAsync(
        MigrationTarget target,
        string? rootDirectory,
        string? migrationsDirectory,
        string? pluginsDirectory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default
    )
        => RunAsync(true, target, rootDirectory, migrationsDirectory, pluginsDirectory, output, error, cancellationToken);

    private static async Task<int> RunAsync(
        bool apply,
        MigrationTarget target,
        string? rootDirectory,
        string? migrationsDirectory,
        string? pluginsDirectory,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var root = (rootDirectory ??
                        Environment.GetEnvironmentVariable("MOONGATE_ROOT") ??
                        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")))
                .ResolvePathAndEnvs();
            string? configuredMigrations = null;
            var migrations =
                (migrationsDirectory ?? Path.Combine(AppContext.BaseDirectory, "..", "migrations")).ResolvePathAndEnvs();
            string connectionString;

            try
            {
                var config = await TomlUtils.DeserializeFromFileAsync<RunnerConfiguration>(
                                 Path.Combine(root, "config", "moongate.toml"),
                                 cancellationToken: cancellationToken
                             );
                configuredMigrations = config?.Persistence.MigrationsDirectory;
                var template = target == MigrationTarget.Auth
                                   ? config?.Persistence.Accounts.ConnectionString
                                   : config?.Persistence.Realm.ConnectionString;

                if (string.IsNullOrWhiteSpace(template))
                {
                    throw new InvalidOperationException();
                }

                connectionString = new NpgsqlConnectionStringBuilder(
                    PostgreSqlConnectionString.Normalize(template.ExpandEnvironmentVariables(true))
                ).ConnectionString;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "Cannot read the selected target's connection_string. Check config/moongate.toml and its environment variables."
                );
            }

            if (migrationsDirectory is null && !string.IsNullOrWhiteSpace(configuredMigrations))
            {
                migrations = configuredMigrations.ExpandEnvironmentVariables(true).ResolvePathAndEnvs();
            }

            var plugins = (pluginsDirectory ?? Path.Combine(root, "plugins")).ResolvePathAndEnvs();
            var catalog = MigrationCatalog.Load(migrations, plugins, target);
            cancellationToken.ThrowIfCancellationRequested();

            if (apply)
            {
                var count = PostgreSqlMigrationRunner.Apply(connectionString, catalog);
                await output.WriteLineAsync($"Applied {count} migration(s) to {target}.");
            }
            else
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                var applied = await MigrationHistory.ReadAsync(() => connection.CreateCommand(), target, cancellationToken);
                var pending = MigrationHistory.Validate(catalog, applied);

                foreach (var script in pending)
                {
                    await output.WriteLineAsync($"Pending {target}: {script.Name}");
                }

                await output.WriteLineAsync($"{pending.Count} pending migration(s) for {target}.");
            }

            return 0;
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync(exception.Message);
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Migration command canceled.");
        }
        catch (Exception)
        {
            await error.WriteLineAsync(
                "Migration command failed. Check PostgreSQL connectivity, permissions and migration files."
            );
        }

        return 1;
    }
}
