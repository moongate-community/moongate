using System.Reflection;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server.Services.Plugins;

/// <summary>
/// Finds the plugins SquidStd loaded from the plugins directory.
/// <para>
/// <c>FromDirectory</c> reports nothing about what it loaded, so they are identified by elimination
/// rather than by path: an assembly outside the server's compile-time graph was not shipped with the
/// server, so it came from that directory. Testing the reference graph rather than the file location
/// avoids guessing how the relative path was resolved, and — unlike a plain "does it implement
/// ISquidStdPlugin" scan — never mistakes a first-party plugin that configuration switched off for an
/// external one that is running.
/// </para>
/// </summary>
public static class PluginDiscovery
{
    /// <summary>Whether <paramref name="candidate" /> lies outside the host's compile-time graph.</summary>
    public static bool IsExternal(Assembly host, Assembly candidate)
    {
        var name = candidate.GetName().Name;

        if (name == host.GetName().Name)
        {
            return false;
        }

        return !host.GetReferencedAssemblies().Any(reference => reference.Name == name);
    }

    /// <summary>Activatable plugin types found in assemblies outside the host's compile-time graph.</summary>
    public static IEnumerable<Type> ExternalPluginTypes(Assembly host, IEnumerable<Assembly> loaded)
        => loaded.Where(assembly => IsExternal(host, assembly))
            .SelectMany(LoadableTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && typeof(ISquidStdPlugin).IsAssignableFrom(type)
            );

    /// <summary>
    /// The types an assembly can actually give up. A plugin built against different dependency versions
    /// throws on <c>GetTypes</c>; the types that did load are still usable, and one broken assembly must
    /// not take down the whole sweep.
    /// </summary>
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
