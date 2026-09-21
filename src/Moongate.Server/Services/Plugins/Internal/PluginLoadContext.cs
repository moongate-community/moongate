using System.Reflection;
using System.Runtime.Loader;

namespace Moongate.Server.Services.Plugins.Internal;

/// <summary>Shares host assemblies and resolves private dependencies within one plugin bundle.</summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _directory;

    internal string BundleDirectory => _directory;

    public PluginLoadContext(string pluginPath)
        : base($"Moongate.Plugin:{Path.GetFileNameWithoutExtension(pluginPath)}", true)
    {
        _resolver = new(pluginPath);
        _directory = Path.GetDirectoryName(pluginPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is "Moongate.Core" or
            "Moongate.Server.Core" or
            "Moongate.Persistence" or
            "Moongate.Persistence.Migrations" or
            "FreeSql" or
            "FreeSql.Provider.PostgreSQL" or
            "Npgsql")
        {
            Assembly host;

            try
            {
                host = Default.LoadFromAssemblyName(new(assemblyName.Name));
            }
            catch (Exception exception) when (exception is FileNotFoundException or FileLoadException)
            {
                throw new FileLoadException(
                    $"Required host persistence contract '{assemblyName}' is unavailable; private copies are not supported.",
                    exception
                );
            }

            var identity = host.GetName();

            if (assemblyName.Version != identity.Version ||
                !string.Equals(
                    assemblyName.CultureName ?? "",
                    identity.CultureName ?? "",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !(assemblyName.GetPublicKeyToken() ?? []).SequenceEqual(identity.GetPublicKeyToken() ?? []))
            {
                throw new FileLoadException(
                    $"Incompatible host persistence contract '{assemblyName}'; host provides '{identity}'. Private copies are not supported."
                );
            }

            return host;
        }

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
