namespace Moongate.Persistence.Tests.TestSupport.Persistence;

internal static class DatabaseBarrier
{
    public static async Task WaitForBlockedReadAsync(PostgreSqlTestDatabase database)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        while (!await database.ScalarAsync<bool>(
                   "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE relation = 'plugin_characters.characters'::regclass AND NOT granted)"
               ))
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
