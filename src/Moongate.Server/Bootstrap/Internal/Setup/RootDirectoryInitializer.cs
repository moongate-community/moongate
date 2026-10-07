using System.Text;
using Moongate.Core.Extensions.Directories;
using Moongate.Core.Utils;
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
        IReadOnlyList<string>? adminCertificateHosts = null,
        string? templatesDirectory = null,
        string? scriptsDirectory = null
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
        var source = BundledMigrations.Load(migrationsDirectory);

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
            Path.Combine(root, ".mgctl.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );
        var destination = Path.Combine(root, "migrations");

        BundledMigrations.EnsureNoConflicts(source, destination);

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
            var target = BundledMigrations.TargetDirectory(catalog.Target);

            foreach (var script in catalog.Scripts)
            {
                CreateIfMissing(
                    Path.Combine(destination, target, script.FileName),
                    File.ReadAllBytes(Path.Combine(migrationsDirectory, target, script.FileName)),
                    output
                );
            }
        }

        // Copy only missing files, so data, templates and scripts the operator edited survive a later run.
        CopyMissing(dataFiles, dataDirectory, Path.Combine(root, "data"), output);

        foreach (var (shipped, name) in new[] { (templatesDirectory, "templates"), (scriptsDirectory, "scripts") })
        {
            if (shipped is not null && Directory.Exists(shipped))
            {
                var files = Directory.GetFiles(shipped, "*", SearchOption.AllDirectories);
                CopyMissing(files, shipped, Path.Combine(root, name), output);
            }
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

    private static void CopyMissing(string[] files, string sourceDirectory, string destination, TextWriter output)
    {
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var path = Path.Combine(destination, Path.GetRelativePath(sourceDirectory, file));
            // Safe: path is combined with a directory, so it always has a parent.
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            CreateIfMissing(path, File.ReadAllBytes(file), output);
        }
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
