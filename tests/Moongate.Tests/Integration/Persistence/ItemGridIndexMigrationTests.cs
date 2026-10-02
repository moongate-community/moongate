using Moongate.Tests.TestSupport.Persistence;
using Npgsql;

namespace Moongate.Tests.Integration.Persistence;

[Collection(PostgresTestCollection.Name)]
public sealed class ItemGridIndexMigrationTests
{
    private const string GridIndexMigration = "0011_item_grid_index.sql";

    [Fact]
    public async Task TheGridIndexMigration_NumbersTheItemsAlreadyInAContainer()
    {
        await using var db = await new PostgreSqlFixture().CreateDatabaseAsync();
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "WorldMigrations"), "*.sql").Order().ToList();
        var migration = files.Single(file => Path.GetFileName(file) == GridIndexMigration);

        foreach (var file in files.TakeWhile(file => file != migration))
        {
            await db.ExecuteAsync(await File.ReadAllTextAsync(file));
        }

        // A world saved before the migration: a chest on the ground with three items, and a loose item beside it.
        await db.ExecuteAsync(
            """
            INSERT INTO world.items (id, template_id, item_id, hue, amount, rarity, map, x, y, z)
            VALUES (1073741824, 'chest', 3649, 0, 1, 0, 1, 100, 100, 0), (1073741830, 'rock', 4963, 0, 1, 0, 1, 101, 100, 0);
            INSERT INTO world.items (id, template_id, item_id, hue, amount, rarity, container_id, grid_x, grid_y)
            VALUES (1073741827, 'c', 3821, 0, 1, 0, 1073741824, 50, 50),
                   (1073741825, 'a', 3821, 0, 1, 0, 1073741824, 50, 50),
                   (1073741826, 'b', 3821, 0, 1, 0, 1073741824, 50, 50);
            """
        );

        await db.ExecuteAsync(await File.ReadAllTextAsync(migration));

        Assert.Equal(
            [(1073741824L, null), (1073741825L, (short?)0), (1073741826L, (short?)1), (1073741827L, (short?)2), (1073741830L, null)],
            await ReadGridIndicesAsync(db)
        );
    }

    private static async Task<List<(long Id, short? GridIndex)>> ReadGridIndicesAsync(PostgreSqlTestDatabase db)
    {
        var rows = new List<(long, short?)>();
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id, grid_index FROM world.items ORDER BY id", connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt16(1)));
        }

        return rows;
    }
}
