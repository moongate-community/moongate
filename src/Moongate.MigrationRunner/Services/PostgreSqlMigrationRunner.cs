using DbUp;
using DbUp.Engine;
using Moongate.MigrationRunner.Internal;
using Moongate.Persistence.Migrations.Services;
using Npgsql;

namespace Moongate.MigrationRunner.Services;

/// <summary>Applies a reviewed catalog as one locked PostgreSQL transaction using DbUp.</summary>
public static class PostgreSqlMigrationRunner
{
    /// <summary>Applies all pending files atomically and returns their count. No database is created.</summary>
    public static int Apply(string connectionString, MigrationCatalog catalog)
    {
        MigrationReviewGuard.Validate(catalog.Scripts);

        foreach (var script in catalog.Scripts)
        {
            TransactionalSql.Validate(script);
        }

        var engine = DeployChanges.To
                                  .PostgresqlDatabase(connectionString)
                                  .WithScripts(
                                      catalog.Scripts.Select(
                                          (script, index) =>
                                              new SqlScript(script.Name, script.Sql, new() { RunGroupOrder = index })
                                      )
                                  )
                                  .JournalTo((manager, _) => new MigrationJournal(manager, catalog))
                                  .WithTransaction()
                                  .WithVariablesDisabled()
                                  .WithExecutionTimeout(TimeSpan.FromSeconds(60))
                                  .LogToNowhere()
                                  .Build();
        var result = engine.PerformUpgrade();

        if (!result.Successful)
        {
            // Keep provider details available to a debugger, out of normal command output.
            var detail = result.Error switch
            {
                PostgresException postgres        => $"PostgreSQL SQLSTATE {postgres.SqlState}.",
                InvalidOperationException invalid => invalid.Message,
                _                                 => "Check PostgreSQL connectivity and permissions."
            };
            var failedScript = result.ErrorScript is null ? "" : $" Script: '{result.ErrorScript.Name}'.";

            throw new InvalidOperationException(
                $"Migration failed; inspect history before retrying.{failedScript} {detail}",
                result.Error
            );
        }

        return result.Scripts.Count();
    }
}
