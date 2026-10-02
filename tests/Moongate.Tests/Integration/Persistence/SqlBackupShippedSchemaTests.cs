using System.Text;
using Moongate.Persistence.Internal;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

/// <summary>
///     The data export against the schema the server really ships: its foreign keys must be orderable, or no shard
///     could be backed up.
/// </summary>
[Collection(PostgresTestCollection.Name)]
public sealed class SqlBackupShippedSchemaTests
{
    [Theory, InlineData("AccountMigrations"), InlineData("WorldMigrations")]
    public async Task TheShippedSchema_Exports(string shipped)
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();

        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, shipped), "*.sql").Order())
        {
            await database.ExecuteAsync(await File.ReadAllTextAsync(file));
        }

        var script = await ExportAsync(database.ConnectionString);

        Assert.Contains("TRUNCATE TABLE ", script);
        Assert.EndsWith("COMMIT;\n", script);
    }

    [Fact]
    public async Task TheShippedWorldSchema_WritesMobilesBeforeTheItemsThatReferenceThem()
    {
        await using var database = await new PostgreSqlFixture().CreateDatabaseAsync();

        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "WorldMigrations"), "*.sql").Order())
        {
            await database.ExecuteAsync(await File.ReadAllTextAsync(file));
        }

        var script = await ExportAsync(database.ConnectionString);

        var mobiles = script.IndexOf("COPY \"world\".\"mobiles\"", StringComparison.Ordinal);
        var items = script.IndexOf("COPY \"world\".\"items\"", StringComparison.Ordinal);
        Assert.InRange(mobiles, 0, items - 1);
    }

    private static async Task<string> ExportAsync(string connectionString)
    {
        await using var output = new MemoryStream();
        await PostgreSqlDataExporter.ExportAsync(connectionString, output, DateTimeOffset.UtcNow, CancellationToken.None);

        return Encoding.UTF8.GetString(output.ToArray());
    }
}
