using Moongate.Server.Core.Exceptions.Admin;
using Moongate.Server.Services.Redis;
using StackExchange.Redis;

namespace Moongate.Server.Services.Admin.Internal;

internal static class AdminRedisOperation
{
    public static async Task<RedisResult> EvaluateAsync(RedisConnectionService redis, string script,
        RedisKey[] keys, RedisValue[] values, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            // Await Redis completion before releasing a caller's SQL lock; cancellation must not
            // let an already queued security mutation run after that lock has been released.
            var result = await redis.Connection.GetDatabase().ScriptEvaluateAsync(script, keys, values).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception exception) when (exception is RedisException or InvalidOperationException or ObjectDisposedException)
        {
            throw new AdminDependencyUnavailableException();
        }
    }
}
