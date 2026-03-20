using Moongate.Server.Data.Internal.Plugins;
using Moongate.Server.Interfaces.Services.Plugins;

namespace Moongate.Server.Services.Plugins;

/// <summary>
/// Validates plugin dependencies and computes a deterministic load order.
/// </summary>
public sealed class PluginDependencyGraphService : IPluginDependencyGraphService
{
    public IReadOnlyList<DiscoveredPlugin> ResolveDependencyOrder(IReadOnlyList<DiscoveredPlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);

        var pluginsById = new Dictionary<string, DiscoveredPlugin>(StringComparer.Ordinal);

        foreach (var plugin in plugins)
        {
            var pluginId = plugin.Manifest.Id!;

            if (!pluginsById.TryAdd(pluginId, plugin))
            {
                throw new InvalidOperationException($"Duplicate plugin id '{pluginId}' was discovered.");
            }
        }

        var ordered = new List<DiscoveredPlugin>(plugins.Count);
        var states = new Dictionary<string, VisitState>(StringComparer.Ordinal);

        foreach (var plugin in pluginsById.Values.OrderBy(static plugin => plugin.Manifest.Id, StringComparer.Ordinal))
        {
            Visit(plugin, pluginsById, states, ordered);
        }

        return ordered;
    }

    private static void Visit(
        DiscoveredPlugin plugin,
        IReadOnlyDictionary<string, DiscoveredPlugin> pluginsById,
        IDictionary<string, VisitState> states,
        ICollection<DiscoveredPlugin> ordered
    )
    {
        var pluginId = plugin.Manifest.Id!;

        if (states.TryGetValue(pluginId, out var state))
        {
            if (state == VisitState.Visiting)
            {
                throw new InvalidOperationException($"Plugin dependency cycle detected at '{pluginId}'.");
            }

            if (state == VisitState.Visited)
            {
                return;
            }
        }

        states[pluginId] = VisitState.Visiting;

        foreach (var dependency in plugin.Manifest.Dependencies.OrderBy(static dependency => dependency.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(dependency.Id))
            {
                throw new InvalidOperationException($"Plugin '{pluginId}' declares a dependency without an id.");
            }

            if (!pluginsById.TryGetValue(dependency.Id, out var dependencyPlugin))
            {
                if (dependency.Optional)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Plugin '{pluginId}' requires missing dependency '{dependency.Id}'."
                );
            }

            ValidateVersionRange(pluginId, dependency.Id, dependency.VersionRange, dependencyPlugin.Manifest.Version);
            Visit(dependencyPlugin, pluginsById, states, ordered);
        }

        states[pluginId] = VisitState.Visited;
        ordered.Add(plugin);
    }

    private static void ValidateVersionRange(
        string pluginId,
        string dependencyId,
        string? versionRange,
        string? dependencyVersion
    )
    {
        if (string.IsNullOrWhiteSpace(versionRange))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(dependencyVersion))
        {
            throw new InvalidOperationException(
                $"Plugin '{pluginId}' requires dependency '{dependencyId}' version '{versionRange}', but the dependency has no version."
            );
        }

        if (versionRange.StartsWith(">=", StringComparison.Ordinal))
        {
            var minimumVersionText = versionRange[2..].Trim();

            if (!Version.TryParse(minimumVersionText, out var minimumVersion) ||
                !Version.TryParse(dependencyVersion, out var actualVersion))
            {
                throw new InvalidOperationException(
                    $"Plugin '{pluginId}' declares unsupported version range '{versionRange}' for dependency '{dependencyId}'."
                );
            }

            if (actualVersion < minimumVersion)
            {
                throw new InvalidOperationException(
                    $"Plugin '{pluginId}' requires dependency '{dependencyId}' version '{versionRange}', but found '{dependencyVersion}'."
                );
            }

            return;
        }

        if (!string.Equals(versionRange, dependencyVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Plugin '{pluginId}' requires dependency '{dependencyId}' version '{versionRange}', but found '{dependencyVersion}'."
            );
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }
}
