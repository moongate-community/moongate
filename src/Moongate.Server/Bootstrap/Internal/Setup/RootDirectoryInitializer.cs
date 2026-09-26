using System.Text;
using Moongate.Core.Extensions.Directories;
using Moongate.Core.Utils;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Server.Data.Config;

namespace Moongate.Server.Bootstrap.Internal.Setup;

/// <summary>
///     Prepares a server data root offline, preserving existing configuration, migration history and shard data files.
/// </summary>
internal static class RootDirectoryInitializer
{
    public static void Initialize(
        string rootDirectory,
        string migrationsDirectory,
        string dataDirectory,
        TextWriter output,
        IReadOnlyList<string>? adminCertificateHosts = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(output);

        if (adminCertificateHosts is not null)
        {
            _ = AdminCertificateSetup.NormalizeHosts(adminCertificateHosts);
        }

        var root = Path.GetFullPath(rootDirectory.ResolvePathAndEnvs());
        var source = new[] { MigrationTarget.Auth, MigrationTarget.World }
            .Select(target => MigrationCatalog.Load(migrationsDirectory, null, target))
            .ToArray();

        if (source.All(catalog => catalog.Scripts.Count == 0))
        {
            throw new InvalidOperationException("The Moongate distribution contains no base SQL migrations.");
        }

        var dataFiles = Directory.Exists(dataDirectory)
                            ? Directory.GetFiles(dataDirectory, "*", SearchOption.AllDirectories)
                            : [];

        if (dataFiles.Length == 0)
        {
            throw new InvalidOperationException("The Moongate distribution contains no shard data files.");
        }

        Directory.CreateDirectory(root);

        // Keep the lock file: unlinking it could give a concurrent initializer a different lock inode.
        using var initializationLock = new FileStream(
            Path.Combine(root, ".mgboot.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        var destination = Path.Combine(root, "migrations");

        foreach (var catalog in source)
        {
            if (!Directory.Exists(destination))
            {
                continue;
            }

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

        foreach (var directory in new[] { "config", "logs", "plugins", "scripts", "migrations/auth", "migrations/world" })
        {
            Directory.CreateDirectory(Path.Combine(root, directory));
        }

        var config = new MoongateServerConfig();
        config.Persistence.MigrationsDirectory = destination;
        config.Validate();
        CreateIfMissing(
            Path.Combine(root, "config/moongate.toml"),
            Encoding.UTF8.GetBytes(TomlUtils.Serialize(config)),
            output
        );

        foreach (var catalog in source)
        {
            var target = catalog.Target == MigrationTarget.Auth ? "auth" : "world";

            foreach (var script in catalog.Scripts)
            {
                CreateIfMissing(
                    Path.Combine(destination, target, script.FileName),
                    File.ReadAllBytes(Path.Combine(migrationsDirectory, target, script.FileName)),
                    output
                );
            }
        }

        // Copy only missing files, so data the operator edited survives a later run.
        foreach (var file in dataFiles.Order(StringComparer.Ordinal))
        {
            var path = Path.Combine(root, "data", Path.GetRelativePath(dataDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            CreateIfMissing(path, File.ReadAllBytes(file), output);
        }

        if (adminCertificateHosts is not null)
        {
            AdminCertificateSetup.Configure(root, adminCertificateHosts, output);
        }

        output.WriteLine($"Root prepared: {root}");
        output.WriteLine(
            "Configure config/moongate.toml, create the PostgreSQL databases, then apply the auth and world migrations."
        );
        output.WriteLine("No database connection or server startup was performed.");
    }

    private static void CreateIfMissing(string path, byte[] content, TextWriter output)
    {
        if (File.Exists(path))
        {
            output.WriteLine($"Preserved: {path}");

            return;
        }

        using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(content);
        }

        output.WriteLine($"Created: {path}");
    }
}
