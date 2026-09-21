using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Data.Events;
using Moongate.Persistence.Migrations.Services;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

public sealed class DevelopmentMigrationTests
{
    [Fact]
    public async Task SamplePlugin_Migrations_AttachSerialSequenceAboveExistingIds()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        var migrations = Path.Combine(AppContext.BaseDirectory, "PluginFixtures/SamplePlugin/migrations/world");
        await db.ExecuteAsync(await File.ReadAllTextAsync(Path.Combine(migrations, "0001_create_notes.sql")));
        await db.ExecuteAsync("INSERT INTO sample_greeter.notes VALUES (42, 'existing')");
        var sequence = await File.ReadAllTextAsync(Path.Combine(migrations, "0002_note_serial_sequence.sql"));
        await db.ExecuteAsync(sequence);
        Assert.Equal(43L, await db.ScalarAsync<long>(
            "SELECT nextval(pg_get_serial_sequence('sample_greeter.notes', 'id'))"
        ));
        await db.ExecuteAsync(sequence);
        Assert.Equal(44L, await db.ScalarAsync<long>(
            "SELECT nextval(pg_get_serial_sequence('sample_greeter.notes', 'id'))"
        ));
    }

    [Fact]
    public async Task InitializeAsync_NewAccountEntityWithUniqueIndex_AppliesOnceWithoutReview()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        for (var startup = 0; startup < 2; startup++)
        {
            await using var coordinator = fixture.Create(
                typeof(Moongate.Server.Ultima.Entities.Auth.AccountEntity),
                Moongate.Persistence.Types.Persistence.PersistenceDatabaseTarget.Accounts
            );
            await coordinator.InitializeAsync();
            Assert.True(coordinator.IsReady);
        }

        var sqlFile = Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, "auth"), "*.sql"));
        Assert.DoesNotContain(MigrationReviewGuard.Marker, await File.ReadAllTextAsync(sqlFile));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
        Assert.Equal("auth.accounts_id_seq", await db.ScalarAsync<string>(
            "SELECT pg_get_serial_sequence('auth.accounts', 'id')"
        ));
        Assert.True(await db.ScalarAsync<bool>(
            "SELECT indisunique FROM pg_index WHERE indexrelid = 'auth.ux_accounts_username'::regclass"
        ));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Bootstrap_ReadinessAndServiceStartFollowSuccessfulMigration(bool blocked)
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        if (blocked)
        {
            Directory.CreateDirectory(Path.Combine(fixture.Migrations, "world"));
            File.WriteAllText(
                Path.Combine(fixture.Migrations, "world/0001_review.sql"),
                MigrationReviewGuard.Marker + "\nSELECT 1;"
            );
        }

        using var container = new Container();
        container.RegisterMoongatePersistence(
            fixture.Config.Persistence.ToOptions(fixture.Migrations, fixture.Plugins, rootDirectory: fixture.Root)
        );
        container.AddPersistenceWorld<TestEntity>();
        List<string> observed = [];
        container.OnEvent<PersistenceReadyEvent>(async (_, _) =>
            {
                if (await db.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history") == 1)
                {
                    observed.Add("ready");
                }
            }
        );
        container.RegisterMoongateService(
            new Moongate.Tests.Support.Server.CallbackStartupService(
                () =>
                {
                    observed.Add("service");
                    return Task.CompletedTask;
                },
                () => Task.CompletedTask
            )
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        try
        {
            if (blocked)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(bootstrap.StartAsync);
                Assert.Empty(observed);
            }
            else
            {
                await bootstrap.StartAsync();
                Assert.Equal(["ready", "service"], observed);
            }
        }
        finally
        {
            await bootstrap.StopAsync();
        }
    }

    [Fact]
    public void Runner_MissingBundleFailsBeforeStartingProcess()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing-runner-" + Guid.NewGuid().ToString("N"));
        var runner = new Moongate.Server.Bootstrap.Internal.DevelopmentMigrationRunner(missing, missing, null, missing);
        Assert.Throws<InvalidOperationException>(runner.ValidateAvailable);
    }

    [Fact]
    public async Task InitializeAsync_ExternalPluginKeepsItsComponentAndDirectory()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        var bundle = Path.Combine(fixture.Plugins, "PersistencePlugin");
        Directory.CreateDirectory(Path.Combine(bundle, "migrations"));
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "PluginFixtures/PersistencePlugin")))
        {
            File.Copy(file, Path.Combine(bundle, Path.GetFileName(file)));
        }

        File.WriteAllText(Path.Combine(bundle, "migrations/manifest.json"), "{\"id\":\"external-data\"}");
        var context =
            new Moongate.Server.Services.Plugins.Internal.PluginLoadContext(Path.Combine(bundle, "PersistencePlugin.dll"));
        try
        {
            var assembly = context.LoadFromAssemblyPath(Path.Combine(bundle, "PersistencePlugin.dll"));
            var entity = assembly.GetType("Moongate.Tests.Fixtures.Plugins.PersistencePlugin.PluginEntity", true)!;
            await using var coordinator = fixture.Create(entity);
            await coordinator.InitializeAsync();
            Assert.Single(Directory.GetFiles(Path.Combine(bundle, "migrations/world"), "*.sql"));
            Assert.False(Directory.Exists(Path.Combine(fixture.Migrations, "world")));
            Assert.Equal("external-data", await db.ScalarAsync<string>("SELECT component FROM moongate_migrations.history"));
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public async Task InitializeAsync_TwoDatabasesSharingSources_DoNotDuplicateScripts()
    {
        await using var firstDb = await new PostgreSqlFixture().CreateDatabaseAsync();
        await using var secondDb = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var firstFiles = new DevelopmentMigrationFixture(firstDb.ConnectionString);
        using var secondFiles = new DevelopmentMigrationFixture(secondDb.ConnectionString);
        secondFiles.Config.Persistence.MigrationsDirectory = firstFiles.Migrations;
        Moongate.Core.Utils.TomlUtils.SerializeToFile(
            secondFiles.Config,
            Path.Combine(secondFiles.Root, "config/moongate.toml")
        );
        await using var first = firstFiles.Create(typeof(TestEntity));
        await using var second = secondFiles.Create(typeof(TestEntity));
        await Task.WhenAll(first.InitializeAsync(), second.InitializeAsync()).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(Directory.GetFiles(Path.Combine(firstFiles.Migrations, "world"), "*.sql"));
        Assert.Equal(1L, await firstDb.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
        Assert.Equal(1L, await secondDb.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
    }

    [Fact]
    public async Task InitializeAsync_CanceledLockWaitReleasesSourceLock()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await using (var connection = new Npgsql.NpgsqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(1296516941)", connection);
            await command.ExecuteNonQueryAsync();
            await using var canceled = fixture.Create(typeof(TestEntity));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.InitializeAsync(cancellation.Token));
            Assert.False(canceled.IsReady);
        }

        await using var retry = fixture.Create(typeof(TestEntity));
        await retry.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(retry.IsReady);
    }

    [Fact]
    public async Task InitializeAsync_FailingPendingSqlRollsBackAndDoesNotGenerate()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        Directory.CreateDirectory(Path.Combine(fixture.Migrations, "world"));
        File.WriteAllText(
            Path.Combine(fixture.Migrations, "world/0001_failure.sql"),
            "CREATE TABLE rolled_back(id integer); SELECT missing_column;"
        );
        await using var coordinator = fixture.Create(typeof(TestEntity));
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());
        Assert.False(coordinator.IsReady);
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql"));
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('rolled_back') IS NOT NULL"));
    }

    [Fact]
    public async Task InitializeAsync_OutputDirectoryIsAFile_FailsBeforeDdl()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        File.WriteAllText(Path.Combine(fixture.Migrations, "world"), "not a directory");
        await using var coordinator = fixture.Create(typeof(TestEntity));
        await Assert.ThrowsAsync<IOException>(() => coordinator.InitializeAsync());
        Assert.False(coordinator.IsReady);
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('host_test.items') IS NOT NULL"));
    }

    [Fact]
    public async Task InitializeAsync_PreexistingUnversionedSchema_RequiresBaseline()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await db.ExecuteAsync(
            "CREATE SCHEMA host_test; CREATE TABLE host_test.items(id bigint PRIMARY KEY, name varchar(255));"
        );
        await using var coordinator = fixture.Create(typeof(DevelopmentItemV2));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());
        Assert.Contains("baseline", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(fixture.Migrations, "*.sql", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task InitializeAsync_ChangedHistoryStopsBeforeGeneratingMoreFiles()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await using (var first = fixture.Create(typeof(TestEntity)))
        {
            await first.InitializeAsync();
        }

        var path = Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql").Single();
        File.AppendAllText(path, "-- changed checksum");
        await using var second = fixture.Create(typeof(DevelopmentItemV2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.InitializeAsync());
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql"));
    }

    [Fact]
    public async Task InitializeAsync_AuthUsesAuthDirectoryAndHistoryTarget()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await using var coordinator = fixture.Create(
            typeof(TestEntity),
            Moongate.Persistence.Types.Persistence.PersistenceDatabaseTarget.Accounts
        );
        await coordinator.InitializeAsync();
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, "auth"), "*.sql"));
        Assert.Equal("auth", await db.ScalarAsync<string>("SELECT target FROM moongate_migrations.history"));
        Assert.False(Directory.Exists(Path.Combine(fixture.Migrations, "world")));
    }

    [Fact]
    public async Task InitializeAsync_CanceledRunnerRollsBackAndLeavesPendingFile()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        var directory = Path.Combine(fixture.Migrations, "world");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "0001_slow.sql");
        File.WriteAllText(path, "CREATE TABLE must_rollback(id integer); SELECT pg_sleep(5);");
        await using var coordinator = fixture.Create(typeof(TestEntity));
        using var cancellation = new CancellationTokenSource();
        var initialization = coordinator.InitializeAsync(cancellation.Token);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!await db.ScalarAsync<bool>(
                   "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname=current_database() AND query LIKE '%pg_sleep(5)%' AND pid<>pg_backend_pid() AND state='active')"
               ))
        {
            Assert.True(DateTime.UtcNow < deadline, "Runner did not begin its transaction.");
            await Task.Delay(30);
        }

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialization);
        Assert.False(coordinator.IsReady);
        Assert.True(File.Exists(path));
        Assert.False(await db.ScalarAsync<bool>("SELECT to_regclass('must_rollback') IS NOT NULL"));
        File.WriteAllText(path, "CREATE TABLE must_rollback(id integer);");
        await using var retry = fixture.Create(typeof(TestEntity));
        await retry.InitializeAsync();
        Assert.True(retry.IsReady);
    }

    [Fact]
    public async Task InitializeAsync_MissingSourceDirectory_IsCreated()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        Directory.Delete(fixture.Migrations);
        await using var coordinator = fixture.Create(typeof(TestEntity));
        await coordinator.InitializeAsync();
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql"));
    }

    [Fact]
    public async Task InitializeAsync_CreateAddRestartAndReplay_UsesVersionedFiles()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await using (var first = fixture.Create(typeof(TestEntity)))
        {
            await first.InitializeAsync();
            Assert.True(first.IsReady);
        }

        await db.ExecuteAsync("INSERT INTO host_test.items(id, name) VALUES (1, 'retained');");
        await using (var second = fixture.Create(typeof(DevelopmentItemV2)))
        {
            await second.InitializeAsync();
        }

        await using (var third = fixture.Create(typeof(DevelopmentItemV2)))
        {
            await third.InitializeAsync();
        }

        Assert.Equal(2, Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql").Length);
        Assert.Equal(
            2L,
            await db.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history WHERE target='world'")
        );
        Assert.Equal("retained", await db.ScalarAsync<string>("SELECT name FROM host_test.items WHERE id=1"));
        await using var other = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var replay = new DevelopmentMigrationFixture(other.ConnectionString);
        Directory.CreateDirectory(Path.Combine(replay.Migrations, "world"));
        foreach (var file in Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql"))
        {
            File.Copy(file, Path.Combine(replay.Migrations, "world", Path.GetFileName(file)));
        }

        await using var restored = replay.Create(typeof(DevelopmentItemV2));
        await restored.InitializeAsync();
        Assert.Equal(2L, await other.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
    }

    [Fact]
    public async Task InitializeAsync_RemovalDraftBlocksRepeatedStartupAndCanBeReviewed()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await using (var first = fixture.Create(typeof(DevelopmentItemV2)))
        {
            await first.InitializeAsync();
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var coordinator = fixture.Create(typeof(TestEntity));
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());
            Assert.False(coordinator.IsReady);
        }

        var files = Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql");
        Assert.Equal(2, files.Length);
        var draft = files.Single(file => Path.GetFileName(file).StartsWith("0002_", StringComparison.Ordinal));
        Assert.Contains(MigrationReviewGuard.Marker, File.ReadAllText(draft));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
        File.WriteAllText(
            draft,
            File.ReadAllText(draft).Replace(MigrationReviewGuard.Marker, "-- Reviewed", StringComparison.Ordinal)
        );
        await using var reviewed = fixture.Create(typeof(TestEntity));
        await reviewed.InitializeAsync();
        Assert.True(reviewed.IsReady);
    }

    [Fact]
    public async Task InitializeAsync_CompetingStarts_OnlyGenerateAndApplyOnce()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        using var fixture = new DevelopmentMigrationFixture(db.ConnectionString);
        await using var first = fixture.Create(typeof(TestEntity));
        await using var second = fixture.Create(typeof(TestEntity));
        await Task.WhenAll(first.InitializeAsync(), second.InitializeAsync()).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Migrations, "world"), "*.sql"));
        Assert.Equal(1L, await db.ScalarAsync<long>("SELECT count(*) FROM moongate_migrations.history"));
    }
}
