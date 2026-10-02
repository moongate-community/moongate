using System.Text;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Tests.Integration.Connections;

[Collection(PostgreSqlCollection.Name)]
public sealed class MoongatePersistenceServiceTests
{
    private readonly PostgreSqlFixture _postgres;

    public MoongatePersistenceServiceTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task InitializeAsync_CanceledConnectionCheck_ReleasesGateAndAllowsRetry()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        using var cancellation = new CancellationTokenSource();
        var cancelDuringResolution = true;
        await using var owner = new MoongatePersistenceService(
            new(
                [
                    new(
                        PersistenceDatabaseTarget.Realm,
                        () =>
                        {
                            if (cancelDuringResolution)
                            {
                                cancellation.Cancel();
                            }

                            return database.ConnectionString;
                        }
                    )
                ]
            )
        );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.InitializeAsync(cancellation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());

        cancelDuringResolution = false;
        await owner.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await owner.SaveAllAsync();
    }

    [Fact]
    public async Task InitializeAsync_NoEntities_ChecksBothTargetsWithoutCreatingTables()
    {
        await using var auth = await _postgres.CreateDatabaseAsync();
        await using var world = await _postgres.CreateDatabaseAsync();
        using var logs = new PersistenceLogCapture();
        await using var owner = new MoongatePersistenceService(
            new(
                [
                    new(PersistenceDatabaseTarget.Accounts, auth.ConnectionString),
                    new(PersistenceDatabaseTarget.Realm, world.ConnectionString)
                ]
            )
        );

        await owner.InitializeAsync();
        await owner.SaveAllAsync();

        var successes = logs.Events
            .Where(e =>
                e.MessageTemplate.Text.StartsWith(
                    "Postgres connection successful",
                    StringComparison.Ordinal
                )
            )
            .ToArray();
        Assert.Equal(2, successes.Length);
        Assert.Contains(
            successes,
            e => e.RenderMessage().Contains("Accounts", StringComparison.Ordinal) &&
                 e.RenderMessage()
                     .Contains(new NpgsqlConnectionStringBuilder(auth.ConnectionString).Database!, StringComparison.Ordinal)
        );
        Assert.Contains(
            successes,
            e => e.RenderMessage().Contains("Realm", StringComparison.Ordinal) &&
                 e.RenderMessage()
                     .Contains(new NpgsqlConnectionStringBuilder(world.ConnectionString).Database!, StringComparison.Ordinal)
        );

        foreach (var database in new[] { auth, world })
        {
            Assert.Equal(
                0L,
                await database.ScalarAsync<long>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')"
                )
            );
        }
    }

    [Theory, InlineData(PersistenceDatabaseTarget.Accounts), InlineData(PersistenceDatabaseTarget.Realm)]
    public async Task InitializeAsync_MissingDatabaseWithoutEntities_ThrowsAndRemainsUnavailable(
        PersistenceDatabaseTarget missingTarget
    )
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var missing = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Database = $"moongate_test_missing_{Guid.NewGuid():N}",
            Password = "synthetic-healthcheck-secret"
        };
        using var logs = new PersistenceLogCapture();
        await using var owner = new MoongatePersistenceService(
            new(
                [
                    new(
                        PersistenceDatabaseTarget.Accounts,
                        missingTarget == PersistenceDatabaseTarget.Accounts
                            ? missing.ConnectionString
                            : database.ConnectionString
                    ),
                    new(
                        PersistenceDatabaseTarget.Realm,
                        missingTarget == PersistenceDatabaseTarget.Realm
                            ? missing.ConnectionString
                            : database.ConnectionString
                    )
                ]
            )
        );

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());

        Assert.Contains(missingTarget.ToString(), error.Message);
        Assert.DoesNotContain(missing.Password, error.ToString());
        Assert.DoesNotContain(logs.Events, e => e.RenderMessage().Contains(missing.Password, StringComparison.Ordinal));
        Assert.DoesNotContain(
            logs.Events,
            e => e.MessageTemplate.Text.StartsWith("Postgres connection successful", StringComparison.Ordinal) &&
                 e.RenderMessage().Contains(missingTarget.ToString(), StringComparison.Ordinal)
        );
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());
    }

    [Fact]
    public async Task InitializeAsync_FailedReinitialization_ClearsReadinessAndCanRetry()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var current = database.ConnectionString;
        await using var owner = new MoongatePersistenceService(
            new(
                [
                    new(PersistenceDatabaseTarget.Realm, () => current)
                ]
            )
        );
        await owner.InitializeAsync();
        current = new NpgsqlConnectionStringBuilder(current) { Database = $"moongate_test_missing_{Guid.NewGuid():N}" }
            .ConnectionString;

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.InitializeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.SaveAllAsync());

        current = database.ConnectionString;
        await owner.InitializeAsync();
        await owner.SaveAllAsync();
    }

    [Fact]
    public async Task ExportDataAsync_AfterInitialization_WritesTheScriptOfThatTarget()
    {
        await using var auth = await _postgres.CreateDatabaseAsync();
        await using var world = await _postgres.CreateDatabaseAsync();
        await world.ExecuteAsync("CREATE TABLE things (id int PRIMARY KEY); INSERT INTO things VALUES (42);");
        await using var owner = new MoongatePersistenceService(
            new(
                [
                    new(PersistenceDatabaseTarget.Accounts, auth.ConnectionString),
                    new(PersistenceDatabaseTarget.Realm, world.ConnectionString)
                ]
            )
        );
        await owner.InitializeAsync();
        IPersistenceDataExporter exporter = owner;
        await using var output = new MemoryStream();

        await exporter.ExportDataAsync(PersistenceDatabaseTarget.Realm, output);

        var script = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("COPY \"public\".\"things\" (\"id\") FROM stdin;\n42\n\\.\n", script);
        Assert.Equal(
            [PersistenceDatabaseTarget.Accounts, PersistenceDatabaseTarget.Realm],
            exporter.ConfiguredTargets.Order()
        );
    }

    [Fact]
    public async Task ExportDataAsync_BeforeInitialization_IsRejected()
    {
        await using var world = await _postgres.CreateDatabaseAsync();
        await using var owner = new MoongatePersistenceService(
            new([new(PersistenceDatabaseTarget.Realm, world.ConnectionString)])
        );
        await using var output = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => owner.ExportDataAsync(PersistenceDatabaseTarget.Realm, output)
        );
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public async Task ExportDataAsync_ATargetThatIsNotConfigured_IsRejected()
    {
        await using var world = await _postgres.CreateDatabaseAsync();
        await using var owner = new MoongatePersistenceService(
            new([new(PersistenceDatabaseTarget.Realm, world.ConnectionString)])
        );
        await owner.InitializeAsync();
        await using var output = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => owner.ExportDataAsync(PersistenceDatabaseTarget.Accounts, output)
        );
    }
}
