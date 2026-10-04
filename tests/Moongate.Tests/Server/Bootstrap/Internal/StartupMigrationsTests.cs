using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Data.Config.Sections;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class StartupMigrationsTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();
    private readonly RecordingMigrationRunner _runner = new();
    private readonly string _bundled;
    private readonly string _migrations;

    public StartupMigrationsTests()
    {
        _bundled = Path.Combine(_directory.Path, "distribution-migrations");
        _migrations = Path.Combine(_directory.Path, "root/migrations");
        Directory.CreateDirectory(Path.Combine(_bundled, "auth"));
        Directory.CreateDirectory(Path.Combine(_bundled, "world"));
        File.WriteAllText(Path.Combine(_bundled, "auth/0001_accounts.sql"), "SELECT 1;\n");
        File.WriteAllText(Path.Combine(_bundled, "world/0001_base.sql"), "SELECT 1;\n");
    }

    [Fact]
    public async Task PrepareAsync_WithAutoApplyOff_TouchesNothing()
    {
        await StartupMigrations.PrepareAsync(
            new PersistenceConfig(),
            _bundled,
            _migrations,
            ServerMode.Standalone,
            _runner,
            CancellationToken.None
        );

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
        await StartupMigrations.PrepareAsync(
            new PersistenceConfig { AutoApplyMigrations = true },
            _bundled,
            _migrations,
            mode,
            _runner,
            CancellationToken.None
        );

        Assert.True(File.Exists(Path.Combine(_migrations, "auth/0001_accounts.sql")));
        Assert.True(File.Exists(Path.Combine(_migrations, "world/0001_base.sql")));
        Assert.Equal(applied, _runner.Applied);
        Assert.Equal(1, _runner.Validated);
    }

    [Fact]
    public async Task PrepareAsync_WithGenerationAlsoOn_OnlyCopies_TheDevelopmentStartApplies()
    {
        await StartupMigrations.PrepareAsync(
            new PersistenceConfig { AutoApplyMigrations = true, AutoGenerateMigrations = true, MigrationsDirectory = _migrations },
            _bundled,
            _migrations,
            ServerMode.Standalone,
            _runner,
            CancellationToken.None
        );

        Assert.True(File.Exists(Path.Combine(_migrations, "world/0001_base.sql")));
        Assert.Empty(_runner.Applied);
    }

    [Fact]
    public async Task PrepareAsync_AConflictingFileInTheRoot_StopsBeforeApplying()
    {
        Directory.CreateDirectory(Path.Combine(_migrations, "world"));
        File.WriteAllText(Path.Combine(_migrations, "world/0001_auto_schema.sql"), "SELECT 1;\n");

        await Assert.ThrowsAsync<InvalidOperationException>(() => StartupMigrations.PrepareAsync(
                new PersistenceConfig { AutoApplyMigrations = true },
                _bundled,
                _migrations,
                ServerMode.Standalone,
                _runner,
                CancellationToken.None
            )
        );

        Assert.Empty(_runner.Applied);
    }

    public void Dispose()
    {
        _directory.Dispose();
    }
}
