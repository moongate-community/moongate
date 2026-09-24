using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Plugins;
using Moongate.Server.Services.Plugins.Internal;
using Serilog;

namespace Moongate.Server.Services.Plugins;

/// <summary>Loads plugin bundles before the bootstrap captures startup service registrations.</summary>
public sealed class PluginLoaderService : IPluginLoaderService, IDisposable
{
    private readonly Lock _sync = new();
    private readonly ILogger _logger = Log.ForContext<PluginLoaderService>();
    private readonly DirectoriesConfig _directories;
    private readonly MoongatePluginRegistry _registry;
    private readonly List<PluginLoadContext> _contexts = [];
    private bool _loaded;
    private bool _loading;
    private bool _faulted;
    private bool _disposed;

    /// <inheritdoc />
    public IReadOnlyList<MoongatePluginData> Plugins => _registry.Plugins;

    public PluginLoaderService(Container container, DirectoriesConfig directories)
    {
        _directories = directories;

        if (!container.IsRegistered<MoongatePluginRegistry>())
        {
            container.RegisterInstance(new MoongatePluginRegistry(container));
        }

        _registry = container.Resolve<MoongatePluginRegistry>();
    }

    /// <inheritdoc />
    public void LoadPlugins()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_loading || _faulted)
            {
                throw new InvalidOperationException(
                    "Plugin loading is already in progress or previously failed. Discard a failed host container."
                );
            }

            if (_loaded)
            {
                return;
            }

            _loading = true;

            try
            {
                var root = Path.GetFullPath(_directories["plugins"]);
                var directories = Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal).ToArray();
                List<IMoongatePlugin> plugins = [];

                foreach (var directory in directories)
                {
                    plugins.AddRange(LoadBundle(directory));
                }

                _registry.Register(plugins);
                _loaded = true;
                _logger.Information("Loaded {PluginCount} disk plugins from {PluginDirectory}", plugins.Count, root);
            }
            catch
            {
                _faulted = true;

                // Rollback callbacks may still need plugin dependencies; disposal owns unloading.
                throw;
            }
            finally
            {
                _loading = false;
            }
        }
    }

    private List<IMoongatePlugin> LoadBundle(string directory)
    {
        var path = Path.Combine(directory, Path.GetFileName(directory) + ".dll");

        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The plugin entry assembly is missing.", path);
            }

            var context = new PluginLoadContext(path);
            _contexts.Add(context);
            var types = context.LoadFromAssemblyPath(path)
                               .GetExportedTypes()
                               .Where(
                                   type => type.IsClass &&
                                           !type.IsAbstract &&
                                           !type.ContainsGenericParameters &&
                                           typeof(IMoongatePlugin).IsAssignableFrom(type)
                               )
                               .OrderBy(type => type.FullName, StringComparer.Ordinal)
                               .ToArray();

            if (types.Length == 0)
            {
                throw new InvalidOperationException("The assembly contains no public concrete IMoongatePlugin types.");
            }

            List<IMoongatePlugin> plugins = [];

            foreach (var type in types)
            {
                var constructor = type.GetConstructor(Type.EmptyTypes) ??
                                  throw new InvalidOperationException(
                                      $"Plugin '{type.FullName}' needs a public parameterless constructor."
                                  );
                plugins.Add((IMoongatePlugin)constructor.Invoke(null));
            }

            return plugins;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Failed to load plugin bundle '{path}'.", exception);
        }
    }

    private List<Exception> UnloadContexts()
    {
        var contexts = _contexts.ToArray();
        _contexts.Clear();
        List<Exception> failures = [];

        foreach (var context in contexts)
        {
            try
            {
                context.Unload();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    /// <summary>Releases plugin load contexts after their services and subscriptions have stopped.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_loading)
            {
                throw new InvalidOperationException("Cannot dispose the plugin loader from a plugin registration callback.");
            }

            _disposed = true;
            var failures = UnloadContexts();

            if (failures.Count > 0)
            {
                throw new AggregateException(failures);
            }
        }
    }
}
