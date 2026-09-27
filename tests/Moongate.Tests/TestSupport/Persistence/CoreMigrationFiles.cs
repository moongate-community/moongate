namespace Moongate.Tests.TestSupport.Persistence;

/// <summary>
///     Applies the SQL shipped under <c>migrations/auth</c> or <c>migrations/world</c>, copied next to the tests, to a
///     test database, in file order.
/// </summary>
internal static class CoreMigrationFiles
{
    public static async Task ApplyAsync(PostgreSqlTestDatabase database, string target)
    {
        var folder = target == "auth" ? "AccountMigrations" : "WorldMigrations";

        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, folder), "*.sql").Order())
        {
            await database.ExecuteAsync(await File.ReadAllTextAsync(file));
        }
    }
}
