using System.Globalization;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Exceptions.Admin;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Admin.Internal;
using Moongate.Server.Services.Redis;
using StackExchange.Redis;

namespace Moongate.Server.Services.Admin;

/// <summary>
///     Atomically validates opaque administrative sessions against Redis authorization gates.
/// </summary>
public sealed class RedisAdminSessionStore : IAdminSessionStore
{
    private readonly RedisConnectionService _redis;
    private readonly string _prefix;

    public RedisAdminSessionStore(RedisConnectionService redis) : this(redis, "moongate:admin:")
    {
    }

    internal RedisAdminSessionStore(RedisConnectionService redis, string prefix)
    {
        _redis = redis;
        _prefix = prefix;
    }

    public async Task<AdminAccountGate?> ReadGateAsync(Serial accountId, CancellationToken token = default)
    {
        ValidateId(accountId);
        var result = (RedisResult[]?)await EvalAsync(AdminRedisScripts.ReadGate, [Gate(accountId)], [], token);

        return result is { Length: 2 } &&
               Guid.TryParseExact((string?)result[0], "N", out var generation) &&
               (string?)result[1] is "0" or "1"
            ? new(generation, (string?)result[1] == "1")
            : null;
    }

    public async Task<AdminAccountGate> ResetGateAsync(Serial accountId, bool blocked, CancellationToken token = default)
    {
        ValidateId(accountId);
        var generation = Guid.NewGuid();
        await EvalAsync(
            AdminRedisScripts.ResetGate,
            [Gate(accountId), Index(accountId)],
            [generation.ToString("N"), blocked ? "1" : "0"],
            token
        );

        return new(generation, blocked);
    }

    public async Task<bool> TryOpenGateAsync(Serial accountId, Guid generation, CancellationToken token = default)
    {
        ValidateId(accountId);

        return (long)await EvalAsync(AdminRedisScripts.OpenGate, [Gate(accountId)], [generation.ToString("N")], token) == 1;
    }

    public async Task<AdminSession> IssueAsync(
        AdminIdentity identity,
        Guid generation,
        string tokenHash,
        TimeSpan lifetime,
        CancellationToken token = default
    )
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateId(identity.AccountId);

        if (!Enum.IsDefined(identity.AccountType) ||
            string.IsNullOrWhiteSpace(identity.Username) ||
            identity.Username.Length > 255 ||
            generation == Guid.Empty ||
            lifetime <= TimeSpan.Zero ||
            lifetime > TimeSpan.FromDays(1))
        {
            throw new ArgumentException("Invalid administration session values.");
        }

        var expires = (long)await EvalAsync(
            AdminRedisScripts.Issue,
            [Gate(identity.AccountId), Index(identity.AccountId), Session(tokenHash)],
            [
                generation.ToString("N"), identity.AccountId.Value, identity.Username,
                (int)identity.AccountType,
                checked((long)Math.Ceiling(lifetime.TotalMilliseconds))
            ],
            token
        );

        if (expires == -2)
        {
            throw new AdminSessionLimitException();
        }

        if (expires < 0)
        {
            throw new AdminSessionRejectedException();
        }

        return new(identity, generation, DateTimeOffset.FromUnixTimeMilliseconds(expires));
    }

    public async Task<AdminSession?> FindAsync(string tokenHash, CancellationToken token = default)
    {
        var result = await EvalAsync(AdminRedisScripts.Find, [Session(tokenHash)], [_prefix], token);

        if (result.IsNull)
        {
            return null;
        }

        var values = (RedisResult[]?)result;

        if (values is not { Length: 5 } ||
            !uint.TryParse((string?)values[0], out var id) ||
            id == 0 ||
            string.IsNullOrWhiteSpace((string?)values[1]) ||
            ((string?)values[1])!.Length > 255 ||
            !int.TryParse((string?)values[2], out var role) ||
            !Enum.IsDefined((AccountType)role) ||
            !Guid.TryParseExact((string?)values[3], "N", out var generation) ||
            generation == Guid.Empty ||
            !long.TryParse((string?)values[4], out var expires) ||
            expires < 0 ||
            expires > 253402300799999)
        {
            return null;
        }

        return new(
            new(new(id), (string)values[1]!, (AccountType)role),
            generation,
            DateTimeOffset.FromUnixTimeMilliseconds(expires)
        );
    }

    public async Task RemoveAsync(string tokenHash, CancellationToken token = default)
    {
        await EvalAsync(AdminRedisScripts.Remove, [Session(tokenHash)], [_prefix], token);
    }

    private Task<RedisResult> EvalAsync(string script, RedisKey[] keys, RedisValue[] values, CancellationToken token)
    {
        return AdminRedisOperation.EvaluateAsync(_redis, script, keys, values, token);
    }

    private string Gate(Serial id)
    {
        return _prefix + "gate:" + id.Value.ToString(CultureInfo.InvariantCulture);
    }

    private string Index(Serial id)
    {
        return _prefix + "index:" + id.Value.ToString(CultureInfo.InvariantCulture);
    }

    private string Session(string digest)
    {
        if (digest.Length != 64 || !digest.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("A SHA-256 token digest is required.", nameof(digest));
        }

        return _prefix + "session:" + digest.ToUpperInvariant();
    }

    private static void ValidateId(Serial id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}
