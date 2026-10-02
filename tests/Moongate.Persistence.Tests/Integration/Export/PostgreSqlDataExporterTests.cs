using System.Text;
using Moongate.Persistence.Data.Internal;
using Moongate.Persistence.Internal;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Npgsql;

namespace Moongate.Persistence.Tests.Integration.Export;

[Collection(PostgreSqlCollection.Name)]
public sealed class PostgreSqlDataExporterTests
{
    private const string Schema =
        """
        CREATE SCHEMA world;
        CREATE TABLE world.mobiles (id bigint PRIMARY KEY, name text NOT NULL);
        CREATE TABLE world.items (
            id bigint PRIMARY KEY,
            name text,
            props jsonb,
            raw bytea,
            created timestamp,
            doubled bigint GENERATED ALWAYS AS (id * 2) STORED,
            container_id bigint REFERENCES world.items (id),
            mobile_id bigint REFERENCES world.mobiles (id)
        );
        CREATE SEQUENCE world.serials START 100;
        CREATE SEQUENCE world.untouched START 7;
        """;

    private const string Rows =
        """
        INSERT INTO world.mobiles VALUES (1, 'Anna'), (2, E'tab\there');
        INSERT INTO world.items (id, name, props, raw, created, container_id, mobile_id) VALUES
            (1, E'line one\nline two', '{"a": [1, 2]}', '\x00ff10', '2026-10-02 11:30:00', 2, 1),
            (2, E'back\\slash', NULL, NULL, NULL, NULL, 2),
            (3, '\.', '[]', '\x', '2026-01-01 00:00:00', NULL, NULL);
        SELECT nextval('world.serials'), nextval('world.serials');
        """;

    private const string Digest =
        """
        SELECT (SELECT string_agg(m::text, '|' ORDER BY m.id) FROM world.mobiles m) || '#' ||
               (SELECT string_agg(i::text, '|' ORDER BY i.id) FROM world.items i)
        """;

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 2, 11, 30, 0, TimeSpan.Zero);

    private readonly PostgreSqlFixture _postgres;

    public PostgreSqlDataExporterTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task ExportAsync_ThenReplay_RestoresEveryRow()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(Schema);
        await database.ExecuteAsync(Rows);
        var before = await database.ScalarAsync<string>(Digest);

        var script = await ExportAsync(database.ConnectionString);
        await database.ExecuteAsync("DELETE FROM world.items WHERE id <> 2; UPDATE world.mobiles SET name = 'changed';");
        await SqlDumpReplayer.ReplayAsync(database.ConnectionString, script);

        Assert.Equal(before, await database.ScalarAsync<string>(Digest));
    }

    [Fact]
    public async Task ExportAsync_Sequences_ComeBackToTheirValue()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(Schema);
        await database.ExecuteAsync(Rows);

        var script = await ExportAsync(database.ConnectionString);
        await database.ExecuteAsync("ALTER SEQUENCE world.serials RESTART; SELECT nextval('world.untouched');");
        await SqlDumpReplayer.ReplayAsync(database.ConnectionString, script);

        Assert.Equal(102L, await database.ScalarAsync<long>("SELECT nextval('world.serials')"));
        Assert.Equal(7L, await database.ScalarAsync<long>("SELECT nextval('world.untouched')"));
    }

    [Fact]
    public async Task ExportAsync_ASequenceRestartedAndNotUsedSince_ComesBackToItsRestartValue()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(Schema);
        await database.ExecuteAsync("ALTER SEQUENCE world.serials RESTART WITH 5000;");

        var script = await ExportAsync(database.ConnectionString);
        await database.ExecuteAsync("ALTER SEQUENCE world.serials RESTART WITH 1;");
        await SqlDumpReplayer.ReplayAsync(database.ConnectionString, script);

        Assert.Equal(5000L, await database.ScalarAsync<long>("SELECT nextval('world.serials')"));
    }

    [Fact]
    public async Task ExportAsync_TheMigrationJournal_IsLeftOut()
    {
        // The journal says which migrations ran on this database: restoring an older one over a migrated
        // database would make the next start run migrations the schema already has.
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(
            """
            CREATE SCHEMA moongate_migrations;
            CREATE TABLE moongate_migrations.history (version int PRIMARY KEY);
            CREATE SEQUENCE moongate_migrations.ids;
            INSERT INTO moongate_migrations.history VALUES (7);
            CREATE TABLE kept (id int PRIMARY KEY);
            """
        );

        var script = await ExportAsync(database.ConnectionString);

        Assert.DoesNotContain("moongate_migrations", script);
        Assert.Contains("COPY \"public\".\"kept\"", script);
    }

    [Fact]
    public async Task ExportAsync_AReferencedTable_IsWrittenBeforeTheTableThatReferencesIt()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(Schema);

        var script = await ExportAsync(database.ConnectionString);

        var mobiles = script.IndexOf("COPY \"world\".\"mobiles\"", StringComparison.Ordinal);
        var items = script.IndexOf("COPY \"world\".\"items\"", StringComparison.Ordinal);
        Assert.InRange(mobiles, 0, items - 1);
    }

    [Fact]
    public async Task ExportAsync_AGeneratedColumn_IsLeftOut()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(Schema);

        var script = await ExportAsync(database.ConnectionString);

        Assert.Contains(
            "COPY \"world\".\"items\" (\"id\", \"name\", \"props\", \"raw\", \"created\", \"container_id\", \"mobile_id\") FROM stdin;",
            script
        );
    }

    [Fact]
    public async Task ExportAsync_TwoTablesReferencingEachOther_FailsNamingThem()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(
            """
            CREATE TABLE a (id int PRIMARY KEY, b_id int);
            CREATE TABLE b (id int PRIMARY KEY, a_id int REFERENCES a (id));
            ALTER TABLE a ADD FOREIGN KEY (b_id) REFERENCES b (id);
            """
        );

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => ExportAsync(database.ConnectionString));

        Assert.Contains("\"public\".\"a\"", failure.Message);
        Assert.Contains("\"public\".\"b\"", failure.Message);
    }

    [Fact]
    public async Task ExportAsync_ARowCommittedAfterTheExportBegan_IsNotInTheFile()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(Schema);
        await database.ExecuteAsync(Rows);
        await using var output = new CallbackStream(() =>
            {
                using var connection = new NpgsqlConnection(database.ConnectionString);
                connection.Open();
                using var command = new NpgsqlCommand("INSERT INTO world.mobiles VALUES (99, 'latecomer')", connection);
                command.ExecuteNonQuery();
            }
        );

        await PostgreSqlDataExporter.ExportAsync(database.ConnectionString, output, CreatedAt, CancellationToken.None);

        Assert.DoesNotContain("latecomer", Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(3L, await database.ScalarAsync<long>("SELECT count(*) FROM world.mobiles"));
    }

    [Fact]
    public async Task ExportAsync_ATableTheRoleCannotRead_IsSkippedAndNamedInTheHeader()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var role = $"moongate_test_ro_{Guid.NewGuid():N}";
        await database.ExecuteAsync(Schema);
        await database.ExecuteAsync(Rows);
        await database.ExecuteAsync(
            $"""
             CREATE ROLE {role} LOGIN PASSWORD 'export-test';
             GRANT USAGE ON SCHEMA world TO {role};
             GRANT SELECT ON world.mobiles TO {role};
             """
        );

        try
        {
            var limited = new NpgsqlConnectionStringBuilder(database.ConnectionString)
            {
                Username = role,
                Password = "export-test"
            };

            var script = await ExportAsync(limited.ConnectionString);

            Assert.Contains("COPY \"world\".\"mobiles\"", script);
            Assert.DoesNotContain("COPY \"world\".\"items\"", script);
            Assert.Contains("-- Skipped (this role cannot read it): \"world\".\"items\"", script);
            Assert.Contains(
                "-- Warning: skipped \"world\".\"items\" references \"world\".\"mobiles\": empty it before the restore, or the TRUNCATE fails.",
                script
            );
            // A sequence the role cannot read has no known value: writing one would reset it on restore.
            Assert.DoesNotContain("setval", script);
        }
        finally
        {
            await database.ExecuteAsync($"DROP OWNED BY {role}; DROP ROLE {role};");
        }
    }

    [Fact]
    public async Task ExportAsync_ATableInASchemaTheRoleCannotUse_IsSkippedInsteadOfFailingTheExport()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var role = $"moongate_test_ro_{Guid.NewGuid():N}";
        await database.ExecuteAsync(Schema);
        await database.ExecuteAsync(Rows);
        await database.ExecuteAsync(
            $"""
             CREATE TABLE kept (id int PRIMARY KEY);
             CREATE ROLE {role} LOGIN PASSWORD 'export-test';
             GRANT SELECT ON kept TO {role};
             GRANT SELECT ON world.mobiles TO {role};
             """
        );

        try
        {
            var limited = new NpgsqlConnectionStringBuilder(database.ConnectionString)
            {
                Username = role,
                Password = "export-test"
            };

            var script = await ExportAsync(limited.ConnectionString);

            Assert.Contains("COPY \"public\".\"kept\"", script);
            Assert.DoesNotContain("COPY \"world\".\"mobiles\"", script);
            Assert.Contains("-- Skipped (this role cannot read it): \"world\".\"mobiles\"", script);
        }
        finally
        {
            await database.ExecuteAsync($"DROP OWNED BY {role}; DROP ROLE {role};");
        }
    }

    [Fact]
    public async Task ExportAsync_AnEmptyDatabase_WritesAScriptThatRuns()
    {
        await using var database = await _postgres.CreateDatabaseAsync();

        var script = await ExportAsync(database.ConnectionString);
        await SqlDumpReplayer.ReplayAsync(database.ConnectionString, script);

        Assert.StartsWith("-- Moongate SQL backup\n", script);
        Assert.Contains("-- Created (UTC): 2026-10-02T11:30:00Z\n", script);
        Assert.DoesNotContain("TRUNCATE", script);
        Assert.DoesNotContain("\r", script);
        Assert.EndsWith("COMMIT;\n", script);
    }

    [Fact]
    public async Task ExportAsync_NamesThatNeedQuoting_Restore()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await database.ExecuteAsync(
            """
            CREATE TABLE "Odd ""Name" ("select" int PRIMARY KEY, "With Space" text);
            INSERT INTO "Odd ""Name" VALUES (1, 'kept');
            """
        );

        var script = await ExportAsync(database.ConnectionString);
        await database.ExecuteAsync("""DELETE FROM "Odd ""Name";""");
        await SqlDumpReplayer.ReplayAsync(database.ConnectionString, script);

        Assert.Equal("kept", await database.ScalarAsync<string>("""SELECT "With Space" FROM "Odd ""Name" """));
    }

    [Fact]
    public async Task ExportAsync_NeverWritesTheConnectionPassword()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var password = new NpgsqlConnectionStringBuilder(database.ConnectionString).Password!;

        var script = await ExportAsync(database.ConnectionString);

        Assert.DoesNotContain(password, script);
    }

    [Fact]
    public void Sort_KeepsNameOrder_WhenNothingReferencesAnything()
    {
        PostgreSqlExportTable[] tables =
        [
            new() { Oid = 1, Schema = "s", Name = "a" },
            new() { Oid = 2, Schema = "s", Name = "b" }
        ];

        var sorted = PostgreSqlDataExporter.Sort(tables, []);

        Assert.Equal(["a", "b"], sorted.Select(table => table.Name));
    }

    private static async Task<string> ExportAsync(string connectionString)
    {
        await using var output = new MemoryStream();
        await PostgreSqlDataExporter.ExportAsync(connectionString, output, CreatedAt, CancellationToken.None);

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
