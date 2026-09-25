using System.Data;
using System.Data.Common;
using DbUp.Engine;
using DbUp.Engine.Transactions;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.MigrationRunner.Internal;

internal sealed class MigrationJournal : IJournal
{
    private readonly Func<IConnectionManager> _connectionManager;
    private readonly MigrationCatalog _catalog;

    public MigrationJournal(Func<IConnectionManager> connectionManager, MigrationCatalog catalog)
    {
        _connectionManager = connectionManager;
        _catalog = catalog;
    }

    public void EnsureTableExistsAndIsLatestVersion(Func<IDbCommand> dbCommandFactory)
    {
        using var command = dbCommandFactory();
        command.CommandText = """
                              CREATE SCHEMA IF NOT EXISTS moongate_migrations;
                              CREATE TABLE IF NOT EXISTS moongate_migrations.history (
                                  target text NOT NULL,
                                  component text NOT NULL,
                                  script text NOT NULL,
                                  checksum varchar(64) NOT NULL,
                                  applied_at timestamptz NOT NULL DEFAULT clock_timestamp(),
                                  PRIMARY KEY (target, component, script)
                              );
                              """;
        command.ExecuteNonQuery();
    }

    public string[] GetExecutedScripts()
    {
        return _connectionManager()
                .ExecuteCommandsWithManagedConnection(
                    factory =>
                    {
                        using var command = factory();
                        command.CommandText =
                            "SET LOCAL standard_conforming_strings = on; SELECT pg_advisory_xact_lock(1296516941)";
                        command.ExecuteNonQuery();
                        var applied = MigrationHistory.ReadAsync(() => (DbCommand)factory(), _catalog.Target)
                                                      .GetAwaiter()
                                                      .GetResult();
                        MigrationHistory.Validate(_catalog, applied);

                        return applied.Select(entry => entry.Name).ToArray();
                    }
                );
    }

    public void StoreExecutedScript(SqlScript script, Func<IDbCommand> dbCommandFactory)
    {
        var migration = _catalog.Scripts.Single(item => item.Name == script.Name);
        using var command = dbCommandFactory();
        command.CommandText =
            "INSERT INTO moongate_migrations.history(target, component, script, checksum) VALUES (@target, @component, @script, @checksum)";
        AddParameter(command, "target", _catalog.Target == MigrationTarget.Auth ? "auth" : "world");
        AddParameter(command, "component", migration.Component);
        AddParameter(command, "script", migration.FileName);
        AddParameter(command, "checksum", migration.Checksum);
        command.ExecuteNonQuery();
    }

    private static void AddParameter(IDbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
