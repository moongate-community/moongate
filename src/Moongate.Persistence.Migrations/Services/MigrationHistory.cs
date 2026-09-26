using System.Data.Common;
using Moongate.Persistence.Migrations.Data.Migrations;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Persistence.Migrations.Services;

/// <summary>
///     Checks immutable history and identifies pending SQL before any mutation.
/// </summary>
public static class MigrationHistory
{
    /// <summary>
    ///     Reads history through the caller's connection and transaction without creating schema objects.
    /// </summary>
    public static async Task<IReadOnlyList<AppliedMigration>> ReadAsync(
        Func<DbCommand> createCommand,
        MigrationTarget target,
        CancellationToken cancellationToken = default
    )
    {
        await using var exists = createCommand();
        exists.CommandText = "SELECT to_regclass('moongate_migrations.history') IS NOT NULL";

        if (!Equals(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), true))
        {
            return [];
        }

        await using var command = createCommand();
        command.CommandText =
            "SELECT component || '/' || script, checksum FROM moongate_migrations.history WHERE target = @target ORDER BY component, script";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "target";
        parameter.Value = target == MigrationTarget.Auth ? "auth" : "world";
        command.Parameters.Add(parameter);
        List<AppliedMigration> result = [];
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new(reader.GetString(0), reader.GetString(1)));
        }

        return result;
    }

    /// <summary>
    ///     Validates every installed component before returning pending scripts in catalog order.
    /// </summary>
    public static IReadOnlyList<MigrationScript> Validate(MigrationCatalog catalog, IReadOnlyList<AppliedMigration> applied)
    {
        var known = catalog.Scripts.ToDictionary(script => script.Name, StringComparer.Ordinal);
        var executed = new HashSet<string>(StringComparer.Ordinal);
        var lastSequences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var entry in applied)
        {
            var separator = entry.Name.IndexOf('/');

            if (separator < 1 || !executed.Add(entry.Name))
            {
                throw new InvalidOperationException("Invalid or duplicate migration history identity.");
            }

            var component = entry.Name[..separator];

            if (!catalog.Components.Contains(component))
            {
                continue;
            }

            if (!known.TryGetValue(entry.Name, out var script))
            {
                throw new InvalidOperationException(
                    $"Applied migration '{entry.Name}' is missing. Restore the original SQL file."
                );
            }

            if (!string.Equals(script.Checksum, entry.Checksum, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Applied migration '{entry.Name}' has changed. Restore it and add a new migration."
                );
            }

            lastSequences[component] = Math.Max(lastSequences.GetValueOrDefault(component), script.Sequence);
        }

        var pending = catalog.Scripts.Where(script => !executed.Contains(script.Name)).ToArray();

        foreach (var script in pending)
        {
            if (script.Sequence <= lastSequences.GetValueOrDefault(script.Component))
            {
                throw new InvalidOperationException(
                    $"Migration '{script.Name}' precedes an applied migration. Use a new sequence."
                );
            }
        }

        return pending;
    }
}
