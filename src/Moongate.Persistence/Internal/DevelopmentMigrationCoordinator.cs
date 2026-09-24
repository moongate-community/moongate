using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Types.Persistence;
using Npgsql;
using Serilog;

namespace Moongate.Persistence.Internal;

internal sealed class DevelopmentMigrationCoordinator
{
    private readonly DevelopmentMigrationOptions _options;
    private readonly ILogger _logger;

    public DevelopmentMigrationCoordinator(DevelopmentMigrationOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(
        IReadOnlyDictionary<PersistenceDatabaseTarget, PostgreSqlDatabase> databases,
        PersistenceModuleRegistrySnapshot snapshot,
        CancellationToken cancellationToken
    )
    {
        _options.Runner.ValidateAvailable();
        _logger.Warning(
            "Development automatic SQL migration generation is enabled; source directory: {MigrationDirectory}",
            _options.Directory
        );

        foreach (var (target, database) in databases.OrderBy(pair => pair.Key))
        {
            var generated = false;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var catalog = _options.Load(target);
                var requiresReview = false;

                using (await MigrationDirectoryLock.AcquireAsync(catalog.SourceDirectories.Values, cancellationToken)
                                                   .ConfigureAwait(false))
                {
                    await using (var databaseLock =
                                 await AcquireDatabaseLockAsync(database, cancellationToken).ConfigureAwait(false))
                    {
                        catalog = _options.Load(target);
                        var history = await MigrationHistory.ReadAsync(
                                                                () => databaseLock.CreateCommand(),
                                                                catalog.Target,
                                                                cancellationToken
                                                            )
                                                            .ConfigureAwait(false);
                        var pending = MigrationHistory.Validate(catalog, history);
                        MigrationReviewGuard.Validate(pending);

                        if (pending.Count == 0)
                        {
                            var groups = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

                            foreach (var module in snapshot.Modules.Where(module => module.Module.DatabaseTarget == target))
                            {
                                var owners = module.EntityTypes
                                                   .Select(type => _options.ResolveComponent(type, catalog))
                                                   .Distinct(StringComparer.Ordinal)
                                                   .ToArray();

                                if (owners.Length != 1 || !catalog.SourceDirectories.ContainsKey(owners[0]))
                                {
                                    throw new InvalidOperationException(
                                        $"Module '{module.Module.Id}' must belong to one known migration component."
                                    );
                                }

                                if (!groups.TryGetValue(owners[0], out var types))
                                {
                                    groups.Add(owners[0], types = []);
                                }

                                types.AddRange(module.EntityTypes);
                            }

                            var changes = new Dictionary<string, DevelopmentSchemaAssessment>(StringComparer.Ordinal);

                            foreach (var (component, types) in groups)
                            {
                                var assessment = await DevelopmentSchemaAssessor
                                                       .AssessAsync(database, types.ToArray(), cancellationToken)
                                                       .ConfigureAwait(false);

                                if (assessment.HasExistingTables &&
                                    !catalog.Scripts.Any(script => script.Component == component))
                                {
                                    throw new InvalidOperationException(
                                        $"Component '{component}' has an existing schema without a versioned baseline. Create and review its baseline before enabling automatic generation."
                                    );
                                }

                                if (!string.IsNullOrWhiteSpace(assessment.Ddl))
                                {
                                    changes.Add(component, assessment);
                                }
                            }

                            if (changes.Count == 0)
                            {
                                break;
                            }

                            if (generated)
                            {
                                throw new InvalidOperationException(
                                    $"Schema differences remain for {target} after applying generated migrations. Review the entity mapping before retrying."
                                );
                            }

                            requiresReview = changes.Values.Any(change => change.RequiresReview);

                            // Mark the whole target batch when one component needs review, including after a partial file write.
                            foreach (var (component, assessment) in changes.OrderBy(
                                         pair => pair.Key,
                                         StringComparer.Ordinal
                                     ))
                            {
                                var file = await MigrationDraftWriter.WriteAsync(
                                                                         catalog,
                                                                         component,
                                                                         assessment.Ddl,
                                                                         requiresReview,
                                                                         cancellationToken
                                                                     )
                                                                     .ConfigureAwait(false);
                                _logger.Information(
                                    "Generated {Target} SQL migration for {Component}: {MigrationFile}; requires review: {RequiresReview}",
                                    target,
                                    component,
                                    file,
                                    requiresReview
                                );
                            }

                            generated = true;
                        }
                    }
                }

                if (requiresReview)
                {
                    throw new InvalidOperationException(
                        $"Generated {target} migrations require review: destructive, ambiguous or unsupported changes were found. Review the SQL and remove '{MigrationReviewGuard.Marker}' before restarting."
                    );
                }

                // The runner takes the same advisory lock; never hold the parent lock while waiting for the child.
                await _options.Runner.ApplyAsync(target, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.Information("Development SQL migrations are up to date");
    }

    private static async Task<NpgsqlConnection> AcquireDatabaseLockAsync(
        PostgreSqlDatabase database,
        CancellationToken cancellationToken
    )
    {
        var builder = new NpgsqlConnectionStringBuilder(database.SchemaConnectionString) { Pooling = false };
        var connection = new NpgsqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            while (true)
            {
                await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);

                // Same single-key bigint lock as MigrationJournal's transaction lock in the isolated runner.
                command.Parameters.AddWithValue("key", 1296516941L);

                if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
                {
                    return connection;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);

            throw;
        }
    }
}
