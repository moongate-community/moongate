using Moongate.Core.Extensions.Directories;
using Moongate.Core.Extensions.Env;
using Moongate.Core.Utils;
using Moongate.MigrationRunner.Data.Internal;
using Moongate.MigrationRunner.Services;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Npgsql;

namespace Moongate.MigrationRunner.Internal;

internal static class MigrationCommand
{
    public static async Task<int> ExecuteAsync(
        string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default
    )
    {
        const string usage =
            "Usage: Moongate.MigrationRunner status|apply --target auth|world [--root-directory PATH] [--migrations-directory PATH]";
        if (args is ["--help"] or ["-h"])
        {
            await output.WriteLineAsync(usage);
            return 0;
        }

        try
        {
            if (args.Length == 0 || args[0] is not ("status" or "apply"))
            {
                throw new InvalidOperationException(usage);
            }

            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 1; index < args.Length; index += 2)
            {
                if (index + 1 >= args.Length ||
                    args[index] is not ("--target" or "--root-directory" or "--migrations-directory") ||
                    !options.TryAdd(args[index], args[index + 1]))
                {
                    throw new InvalidOperationException(usage);
                }
            }

            var target = options.GetValueOrDefault("--target") switch
            {
                "auth"  => MigrationTarget.Auth,
                "world" => MigrationTarget.World,
                _       => throw new InvalidOperationException(usage)
            };
            var root = (options.GetValueOrDefault("--root-directory") ??
                        Environment.GetEnvironmentVariable("MOONGATE_ROOT") ??
                        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")))
                .ResolvePathAndEnvs();
            var migrations =
                (options.GetValueOrDefault("--migrations-directory") ??
                 Path.Combine(AppContext.BaseDirectory, "..", "migrations")).ResolvePathAndEnvs();
            var catalog = MigrationCatalog.Load(migrations, Path.Combine(root, "plugins"), target);
            string connectionString;
            try
            {
                var config = await TomlUtils.DeserializeFromFileAsync<RunnerConfiguration>(
                    Path.Combine(root, "config", "moongate.toml"),
                    cancellationToken: cancellationToken
                );
                var template = target == MigrationTarget.Auth
                    ? config?.Persistence.Accounts.ConnectionString
                    : config?.Persistence.Realm.ConnectionString;
                if (string.IsNullOrWhiteSpace(template))
                {
                    throw new InvalidOperationException();
                }

                connectionString = new NpgsqlConnectionStringBuilder(
                    PostgreSqlConnectionString.Normalize(template.ExpandEnvironmentVariables(requireDefined: true))
                ).ConnectionString;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "Cannot read the selected target's connection_string. Check config/moongate.toml and its environment variables."
                );
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (args[0] == "apply")
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
