using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Moongate.Persistence.Migrations.Data.Migrations;
using Moongate.Persistence.Migrations.Types.Migrations;

namespace Moongate.Persistence.Migrations.Services;

/// <summary>Discovers versioned core and plugin SQL without loading plugin assemblies.</summary>
public sealed partial class MigrationCatalog
{
    /// <summary>Gets the database target shared by every script.</summary>
    public MigrationTarget Target { get; }

    /// <summary>Gets core scripts first, then plugins by stable ID, with ascending component sequences.</summary>
    public IReadOnlyList<MigrationScript> Scripts { get; }

    /// <summary>Gets installed components, including those with no SQL for this target.</summary>
    public IReadOnlySet<string> Components { get; }

    /// <summary>Gets the absolute migrations root for each installed component.</summary>
    public IReadOnlyDictionary<string, string> SourceDirectories { get; }

    private MigrationCatalog(MigrationTarget target, List<MigrationScript> scripts, HashSet<string> components,
        IReadOnlyDictionary<string, string> sources)
    {
        SourceDirectories = sources.ToFrozenDictionary(StringComparer.Ordinal);
        Target = target;
        Scripts = scripts.AsReadOnly();
        Components = components.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>Reads a required core directory and optional plugin bundles into an immutable catalog.</summary>
    public static MigrationCatalog Load(string directory, string? pluginsDirectory, MigrationTarget target)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target));
        }

        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException("The core migrations directory is missing.");
        }

        var components = new HashSet<string>(StringComparer.Ordinal) { "core" };
        var sources = new SortedDictionary<string, string>(StringComparer.Ordinal);

        if (pluginsDirectory is not null && Directory.Exists(pluginsDirectory))
        {
            foreach (var bundle in Directory.EnumerateDirectories(pluginsDirectory))
            {
                var migrations = Path.Combine(bundle, "migrations");

                if (!Directory.Exists(migrations))
                {
                    continue;
                }

                var manifestPath = Path.Combine(migrations, "manifest.json");

                if (!File.Exists(manifestPath))
                {
                    throw new InvalidOperationException("Plugin migrations require a manifest.json with a stable id.");
                }

                using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                var id = manifest.RootElement.TryGetProperty("id", out var property) &&
                         property.ValueKind == JsonValueKind.String
                             ? property.GetString()
                             : null;

                if (id is null || !ComponentPattern().IsMatch(id) || !components.Add(id))
                {
                    throw new InvalidOperationException(
                        "Migration component IDs must be unique lowercase names; core is reserved."
                    );
                }

                sources.Add(id, Path.GetFullPath(migrations));
            }
        }

        var scripts = ReadComponent(directory, "core", target);

        foreach (var (id, path) in sources)
        {
            scripts.AddRange(ReadComponent(path, id, target));
        }

        sources.Add("core", Path.GetFullPath(directory));
        return new(target, scripts, components, sources);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentPattern();

    [GeneratedRegex("^[0-9]{4}_[a-z][a-z0-9_]*\\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex FilePattern();

    private static List<MigrationScript> ReadComponent(string root, string component, MigrationTarget target)
    {
        var directory = Path.Combine(root, target == MigrationTarget.Auth ? "auth" : "world");
        List<MigrationScript> scripts = [];

        if (!Directory.Exists(directory))
        {
            return scripts;
        }

        if (Directory.EnumerateDirectories(directory).Any())
        {
            throw new InvalidOperationException("Migration target directories must be flat.");
        }

        var sequences = new HashSet<int>();

        foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);

            if (!name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!FilePattern().IsMatch(name) ||
                !int.TryParse(name.AsSpan(0, 4), out var sequence) ||
                sequence == 0 ||
                !sequences.Add(sequence))
            {
                throw new InvalidOperationException(
                    $"Migration '{component}/{name}' needs a unique positive NNNN_lowercase_name.sql sequence."
                );
            }

            var sql = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(file))
                                                   .TrimStart('\uFEFF')
                                                   .Replace("\r\n", "\n", StringComparison.Ordinal);

            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new InvalidOperationException($"Migration '{component}/{name}' is empty.");
            }

            var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            scripts.Add(new(target, component, name, sequence, sql, checksum));
        }

        return scripts;
    }
}
