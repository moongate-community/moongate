using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Server.Bootstrap.Internal.Setup;

/// <summary>
///     The core SQL migrations a distribution ships beside the server, and their copy into a root: files the root
///     lacks are added, files it has are never replaced.
/// </summary>
internal static class BundledMigrations
{
    /// <summary>
    ///     Loads the auth and world catalogs of the distribution.
    /// </summary>
    public static MigrationCatalog[] Load(string source)
    {
        return new[] { MigrationTarget.Auth, MigrationTarget.World }
            .Select(target => MigrationCatalog.Load(source, null, target))
            .ToArray();
    }

    /// <summary>
    ///     Fails when the destination has a file with the number of a bundled one and another name or content.
    /// </summary>
    public static void EnsureNoConflicts(IEnumerable<MigrationCatalog> source, string destination)
    {
        if (!Directory.Exists(destination))
        {
            return;
        }

        foreach (var catalog in source)
        {
            var existing = MigrationCatalog.Load(destination, null, catalog.Target);

            foreach (var script in catalog.Scripts)
            {
                var collision = existing.Scripts.FirstOrDefault(item => item.Sequence == script.Sequence);

                if (collision is not null &&
                    (collision.FileName != script.FileName || collision.Checksum != script.Checksum))
                {
                    throw new InvalidOperationException(
                        $"Existing migration '{collision.FileName}' for {catalog.Target} conflicts with bundled '{script.FileName}'. " +
                        "No files were replaced. Use an empty root or reconcile the migration catalog manually."
                    );
                }
            }
        }
    }

    /// <summary>
    ///     Copies into the destination the bundled files it lacks, after checking that none conflicts, and returns the
    ///     paths it created. A distribution with no bundled migrations copies nothing.
    /// </summary>
    public static IReadOnlyList<string> CopyMissing(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return [];
        }

        var catalogs = Load(source);
        EnsureNoConflicts(catalogs, destination);
        var created = new List<string>();

        foreach (var catalog in catalogs)
        {
            var target = TargetDirectory(catalog.Target);

            foreach (var script in catalog.Scripts)
            {
                var path = Path.Combine(destination, target, script.FileName);

                if (File.Exists(path))
                {
                    continue;
                }

                // Whole or not at all: a start killed half way must not leave a file the next one takes for a conflict.
                var temporary = path + ".tmp";
                // path is combined with a directory, so it always has a parent.
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.Copy(Path.Combine(source, target, script.FileName), temporary, true);
                File.Move(temporary, path);
                created.Add(path);
            }
        }

        return created;
    }

    public static string TargetDirectory(MigrationTarget target)
    {
        return target == MigrationTarget.Auth ? "auth" : "world";
    }
}
