namespace Moongate.Server.Core.Data.Plugins;

/// <summary>Describes a plugin and its required dependencies.</summary>
public sealed record MoongatePluginData
{
    public string Id { get; }
    public string Name { get; }
    public Version Version { get; }
    public string? Author { get; }
    public string? Description { get; }
    public IReadOnlyList<MoongatePluginDependencyData> Dependencies { get; }

    public MoongatePluginData(
        string id,
        string name,
        Version version,
        string? author = null,
        string? description = null,
        IEnumerable<MoongatePluginDependencyData>? dependencies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(version);
        var snapshot = dependencies?.ToArray() ?? [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in snapshot)
        {
            if (dependency is null || !ids.Add(dependency.Id))
            {
                throw new ArgumentException(
                    "Dependencies must be non-null and have unique IDs.", nameof(dependencies));
            }
        }

        Id = id;
        Name = name;
        Version = version;
        Author = author;
        Description = description;
        Dependencies = Array.AsReadOnly(snapshot);
    }
}
