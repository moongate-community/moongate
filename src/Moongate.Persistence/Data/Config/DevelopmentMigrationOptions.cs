using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Migrations.Services;
using Moongate.Persistence.Migrations.Types.Migrations;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Data.Config;

/// <summary>
///     Explicit development-only file sources and isolated execution policy.
/// </summary>
public sealed class DevelopmentMigrationOptions
{
    public string Directory { get; }
    public string? PluginsDirectory { get; }
    public IDevelopmentMigrationRunner Runner { get; }
    public Func<Type, MigrationCatalog, string> ResolveComponent { get; }

    public DevelopmentMigrationOptions(
        string directory,
        string? pluginsDirectory,
        IDevelopmentMigrationRunner runner,
        Func<Type, MigrationCatalog, string> resolveComponent
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory = Path.GetFullPath(directory);
        PluginsDirectory = pluginsDirectory is null ? null : Path.GetFullPath(pluginsDirectory);
        Runner = runner;
        ResolveComponent = resolveComponent;
    }

    public MigrationCatalog Load(PersistenceDatabaseTarget target)
    {
        System.IO.Directory.CreateDirectory(Directory);

        return MigrationCatalog.Load(
            Directory,
            PluginsDirectory,
            target == PersistenceDatabaseTarget.Accounts ? MigrationTarget.Auth : MigrationTarget.World
        );
    }
}
