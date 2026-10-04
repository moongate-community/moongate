using Moongate.Core.Directories;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Config.Sections;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class StartupMigrationsTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly RecordingMigrationRunner _runner = new();
    private readonly List<(string Root, string Migrations, string? Plugins)> _runners = [];
    private readonly string _bundled;
    private readonly string _root;
    private readonly string _migrations;

    public StartupMigrationsTests()
    {
        _bundled = Path.Combine(_directory.Path, "distribution-migrations");
        _root = Path.Combine(_directory.Path, "root");
        _migrations = Path.Combine(_root, "migrations");
        Directory.CreateDirectory(Path.Combine(_bundled, "auth"));
        Directory.CreateDirectory(Path.Combine(_bundled, "world"));
        File.WriteAllText(Path.Combine(_bundled, "auth/0001_accounts.sql"), "SELECT 1;\n");
        File.WriteAllText(Path.Combine(_bundled, "world/0001_base.sql"), "SELECT 1;\n");
    }

    [Fact]
    public async Task PrepareAsync_WithAutoApplyOff_TouchesNothing()
    {
        await PrepareAsync(new());

        Assert.False(Directory.Exists(_migrations));
        Assert.Empty(_runner.Applied);
    }

    [Theory,
     InlineData(ServerMode.Standalone, new[] { PersistenceDatabaseTarget.Accounts, PersistenceDatabaseTarget.Realm }),
     InlineData(ServerMode.Login, new[] { PersistenceDatabaseTarget.Accounts }),
     InlineData(ServerMode.Game, new[] { PersistenceDatabaseTarget.Realm })]
    public async Task PrepareAsync_WithAutoApplyOn_CopiesTheBundledSql_ThenAppliesTheTargetsOfTheMode(
        ServerMode mode,
        PersistenceDatabaseTarget[] applied
    )
    {
        await PrepareAsync(new() { AutoApplyMigrations = true }, mode);

        Assert.True(File.Exists(Path.Combine(_migrations, "auth/0001_accounts.sql")));
        Assert.True(File.Exists(Path.Combine(_migrations, "world/0001_base.sql")));
        Assert.Equal(applied, _runner.Applied);
        Assert.Equal(1, _runner.Validated);
    }

    [Fact]
    public async Task PrepareAsync_WithGenerationAlsoOn_OnlyCopies_TheDevelopmentStartApplies()
    {
        await PrepareAsync(new() { AutoApplyMigrations = true, AutoGenerateMigrations = true, MigrationsDirectory = _migrations });

        Assert.True(File.Exists(Path.Combine(_migrations, "world/0001_base.sql")));
        Assert.Empty(_runner.Applied);
    }

    [Fact]
    public async Task PrepareAsync_AConflictingFileInTheRoot_StopsBeforeApplying()
    {
        Directory.CreateDirectory(Path.Combine(_migrations, "world"));
        File.WriteAllText(Path.Combine(_migrations, "world/0001_auto_schema.sql"), "SELECT 1;\n");

        await Assert.ThrowsAsync<InvalidOperationException>(() => PrepareAsync(new() { AutoApplyMigrations = true }));

        Assert.Empty(_runner.Applied);
    }

    [Fact]
    public async Task PrepareAsync_TheRunnerGetsTheRootItsMigrationsAndItsPlugins_AndAConfiguredDirectoryIsUsed()
    {
        await PrepareAsync(new() { AutoApplyMigrations = true });

        Assert.Equal((_root, _migrations, Path.Combine(_root, "plugins")), Assert.Single(_runners));

        var elsewhere = Path.Combine(_directory.Path, "elsewhere");
        await PrepareAsync(new() { AutoApplyMigrations = true, MigrationsDirectory = elsewhere });

        Assert.Equal((_root, elsewhere, Path.Combine(_root, "plugins")), _runners[^1]);
        Assert.True(File.Exists(Path.Combine(elsewhere, "world/0001_base.sql")));
    }

    private Task PrepareAsync(PersistenceConfig persistence, ServerMode mode = ServerMode.Standalone)
    {
        var config = new MoongateServerConfig { Mode = mode, Persistence = persistence };

        return StartupMigrations.PrepareAsync(
            config,
            new DirectoriesConfig(_root, ["plugins"]),
            _bundled,
            (root, migrations, plugins) =>
            {
                _runners.Add((root, migrations, plugins));

                return _runner;
            },
            CancellationToken.None
        );
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
