using System.Diagnostics;
using DryIoc;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;
using Serilog;

namespace Moongate.Server.Core.Plugins;

/// <summary>Registers explicit plugins in dependency order before server startup.</summary>
public sealed class MoongatePluginRegistry
{
    private readonly Container _container;
    private readonly ILogger _logger = Log.ForContext<MoongatePluginRegistry>();
    private readonly List<MoongatePluginData> _plugins = new();
    private bool _isRegistering;
    private bool _isFaulted;

    public IReadOnlyList<MoongatePluginData> Plugins { get; }

    public MoongatePluginRegistry(Container container)
    {
        _container = container;
        Plugins = _plugins.AsReadOnly();
    }

    public void Register(IMoongatePlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        Register(new[] { plugin });
    }

    public void Register(IEnumerable<IMoongatePlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        if (_isFaulted)
        {
            throw new InvalidOperationException(
                "Plugin registration previously failed. Discard this container and abort startup."
            );
        }

        if (_isRegistering)
        {
            throw new InvalidOperationException("Nested plugin registration is not supported.");
        }

        _isRegistering = true;

        try
        {
            var candidates = plugins.Select(
                                        plugin =>
                                        {
                                            ArgumentNullException.ThrowIfNull(plugin);
                                            var metadata =
                                                plugin.Metadata ??
                                                throw new InvalidOperationException(
                                                    $"Plugin '{plugin.GetType().FullName}' returned null metadata."
                                                );

                                            return (Plugin: plugin, Metadata: metadata);
                                        }
                                    )
                                    .ToArray();
            var ordered = ValidateAndOrder(candidates.Select(entry => entry.Metadata).ToArray());
            var instances = candidates.ToDictionary(
                entry => entry.Metadata.Id,
                entry => entry.Plugin,
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var metadata in ordered)
            {
                try
                {
                    var startWatch = Stopwatch.GetTimestamp();
                    instances[metadata.Id].Register(_container);
                    _logger.Information(
                        "Plugin '{PluginId}' registered in {Elapsed:F6} seconds.",
                        metadata.Id,
                        Stopwatch.GetElapsedTime(startWatch)
                    );
                }
                catch (Exception exception)
                {
                    _isFaulted = true;

                    throw new InvalidOperationException(
                        $"Plugin '{metadata.Id}' failed during registration.",
                        exception
                    );
                }

                _plugins.Add(metadata);
            }
        }
        finally
        {
            _isRegistering = false;
        }
    }

    private List<MoongatePluginData> ValidateAndOrder(IReadOnlyList<MoongatePluginData> candidates)
    {
        var available = _plugins.ToDictionary(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase);
        var pending = new Dictionary<string, MoongatePluginData>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (!available.TryAdd(candidate.Id, candidate))
            {
                throw new InvalidOperationException($"Plugin ID '{candidate.Id}' is already registered or duplicated.");
            }

            pending.Add(candidate.Id, candidate);
        }

        foreach (var candidate in candidates)
        {
            foreach (var dependency in candidate.Dependencies)
            {
                if (!available.TryGetValue(dependency.Id, out var required))
                {
                    throw new InvalidOperationException(
                        $"Plugin '{candidate.Id}' requires missing plugin '{dependency.Id}'."
                    );
                }

                if (dependency.MinimumVersion is not null &&
                    required.Version.CompareTo(dependency.MinimumVersion) < 0)
                {
                    throw new InvalidOperationException(
                        $"Plugin '{candidate.Id}' requires '{dependency.Id}' >= {dependency.MinimumVersion}; " +
                        $"found {required.Version}."
                    );
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();
        var ordered = new List<MoongatePluginData>();

        foreach (var candidate in candidates)
        {
            Visit(candidate);
        }

        return ordered;

        void Visit(MoongatePluginData candidate)
        {
            if (visited.Contains(candidate.Id))
            {
                return;
            }

            if (!visiting.Add(candidate.Id))
            {
                throw new InvalidOperationException(
                    $"Plugin dependency cycle: {string.Join(" -> ", path.Append(candidate.Id))}."
                );
            }

            path.Add(candidate.Id);

            foreach (var dependency in candidate.Dependencies)
            {
                if (pending.TryGetValue(dependency.Id, out var required))
                {
                    Visit(required);
                }
            }

            path.RemoveAt(path.Count - 1);
            visiting.Remove(candidate.Id);
            visited.Add(candidate.Id);
            ordered.Add(candidate);
        }
    }
}
