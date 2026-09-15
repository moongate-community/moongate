using System.Reflection;
using System.Runtime.Loader;

namespace Moongate.Server.Services.Plugins.Internal;

/// <summary>Shares host assemblies and resolves private dependencies within one plugin bundle.</summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _directory;

    public PluginLoadContext(string pluginPath)
        : base($"Moongate.Plugin:{Path.GetFileNameWithoutExtension(pluginPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _directory = Path.GetDirectoryName(pluginPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        try
        {
            // Host contracts and their dependencies must retain the host's type identity.
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (FileNotFoundException)
        {
            // Assemblies not supplied by the host belong to the plugin's private context.
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null && assemblyName.Name is not null)
        {
            var adjacentPath = Path.Combine(_directory, assemblyName.Name + ".dll");
            if (File.Exists(adjacentPath))
            {
                path = adjacentPath;
            }
        }

        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}
