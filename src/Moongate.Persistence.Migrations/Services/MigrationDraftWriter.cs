using System.Text;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Persistence.Migrations.Services;

/// <summary>
///     Publishes a numbered SQL draft atomically without replacing an existing migration.
/// </summary>
public static class MigrationDraftWriter
{
    public static async Task<string> WriteAsync(
        MigrationCatalog catalog,
        string component,
        string sql,
        bool requiresReview,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        var root = catalog.SourceDirectories[component];
        var sequence = catalog.Scripts
                           .Where(script => script.Component == component)
                           .Select(script => script.Sequence)
                           .DefaultIfEmpty()
                           .Max() +
                       1;

        if (sequence > 9999)
        {
            throw new InvalidOperationException($"Migration sequence exhausted for '{component}'.");
        }

        var directory = Path.Combine(root, catalog.Target == MigrationTarget.Auth ? "auth" : "world");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{sequence:D4}_auto_schema.sql");
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        var header = requiresReview ? MigrationReviewGuard.Marker + "\n" : "";

        try
        {
            await File.WriteAllTextAsync(temporary, header + sql + "\n", new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, false);

            return path;
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
