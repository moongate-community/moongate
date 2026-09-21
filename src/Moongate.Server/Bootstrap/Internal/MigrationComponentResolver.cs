using System.Runtime.Loader;
using Moongate.Persistence.Migrations.Services;
using Moongate.Server.Services.Plugins.Internal;

namespace Moongate.Server.Bootstrap.Internal;

internal static class MigrationComponentResolver
{
    public static string Resolve(Type entity, MigrationCatalog catalog)
    {
        var context = AssemblyLoadContext.GetLoadContext(entity.Assembly);
        if (context == AssemblyLoadContext.Default)
        {
            return "core";
        }

        if (context is not PluginLoadContext plugin)
        {
            throw new InvalidOperationException($"Cannot determine migration component for '{entity.FullName}'.");
        }

        var directory = Path.GetFullPath(Path.Combine(plugin.BundleDirectory, "migrations"));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return catalog.SourceDirectories
                   .SingleOrDefault(source => source.Key != "core" && string.Equals(source.Value, directory, comparison))
                   .Key
               ?? throw new InvalidOperationException(
                   $"Plugin entity '{entity.FullName}' needs a migrations/manifest.json with a stable component ID."
               );
    }
}
