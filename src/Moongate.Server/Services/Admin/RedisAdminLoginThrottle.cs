using System.Net;
using System.Security.Cryptography;
using System.Text;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Services.Admin.Internal;
using Moongate.Server.Services.Redis;

namespace Moongate.Server.Services.Admin;

/// <summary>Shares bounded username and direct-peer login attempt windows across hosts.</summary>
public sealed class RedisAdminLoginThrottle : IAdminLoginThrottle
{
    private readonly RedisConnectionService _redis;
    private readonly string _prefix;

    public RedisAdminLoginThrottle(RedisConnectionService redis) : this(redis, "moongate:admin:") { }

    internal RedisAdminLoginThrottle(RedisConnectionService redis, string prefix)
    {
        _redis = redis;
        _prefix = prefix;
    }

    public async Task<bool> TryAcquireAsync(string peerAddress, string username, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        if (username.Length > 255 || !IPAddress.TryParse(peerAddress, out var peer))
        {
            throw new ArgumentException("Invalid administrative login throttle inputs.");
        }
        var result = await AdminRedisOperation.EvaluateAsync(
                         _redis,
                         AdminRedisScripts.Throttle,
                         [
                             _prefix + "throttle:user:" + Digest(username),
                             _prefix + "throttle:peer:" + Digest(peer.MapToIPv6().ToString())
                         ],
                         [],
                         token
                     );

        return (long)result == 1;
    }

    private static string Digest(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
