using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Moongate.Network.Packets.Data.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Services.Realms.Internal;
using Moongate.Server.Services.Redis;
using StackExchange.Redis;

namespace Moongate.Server.Services.Realms;

/// <summary>Publishes fenced, expiring realm leases and reads live realm snapshots from Redis.</summary>
public sealed class RedisRealmDirectoryService : IRealmCatalog, IRealmPresenceService
{
    private const string ClaimScript = """
                                       local owner = redis.call('HGET', KEYS[1], 'realm_id')
                                       if owner and owner ~= ARGV[1] then return -1 end
                                       if owner then
                                           local instance = redis.call('HGET', KEYS[1], 'instance_id')
                                           if ARGV[11] == 'heartbeat' and instance ~= ARGV[2] then return -4 end
                                           if instance == ARGV[2] then
                                               if redis.call('HGET', KEYS[1], 'name') ~= ARGV[4] or
                                                  redis.call('HGET', KEYS[1], 'ipv4') ~= ARGV[5] or
                                                  redis.call('HGET', KEYS[1], 'port') ~= ARGV[6] or
                                                  redis.call('HGET', KEYS[1], 'minimum_account_type') ~= ARGV[7] then
                                                   return -3
                                               end
                                           end
                                       else
                                           local cursor = '0'
                                           local count = 0
                                           repeat
                                               local scan = redis.call('SCAN', cursor, 'MATCH', ARGV[8] .. '*', 'COUNT', 128)
                                               cursor = scan[1]
                                               count = count + #scan[2]
                                               if count >= tonumber(ARGV[9]) then return -2 end
                                           until cursor == '0'
                                       end
                                       redis.call('HSET', KEYS[1],
                                           'realm_id', ARGV[1], 'instance_id', ARGV[2], 'server_index', ARGV[3],
                                           'name', ARGV[4], 'ipv4', ARGV[5], 'port', ARGV[6],
                                           'minimum_account_type', ARGV[7])
                                       redis.call('EXPIRE', KEYS[1], tonumber(ARGV[10]))
                                       if not owner and ARGV[11] == 'heartbeat' then return 2 end
                                       return 1
                                       """;

    private const string UnregisterScript = """
                                            if redis.call('HGET', KEYS[1], 'realm_id') ~= ARGV[1] or
                                               redis.call('HGET', KEYS[1], 'instance_id') ~= ARGV[2] then
                                                return 0
                                            end
                                            return redis.call('DEL', KEYS[1])
                                            """;

    private readonly RedisConnectionService _redis;
    private readonly string _prefix;
    private readonly int _leaseSeconds;
    private readonly int _maxRealms;

    public RedisRealmDirectoryService(
        RedisConnectionService redis,
        string prefix = "moongate:realm:",
        TimeSpan? leaseDuration = null,
        int maxRealms = 128
    )
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Realm key prefix is required.", nameof(prefix));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxRealms, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRealms, 128);
        _redis = redis;
        _prefix = prefix;
        _leaseSeconds = checked((int)(leaseDuration ?? TimeSpan.FromSeconds(15)).TotalSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(_leaseSeconds, 1);
        _maxRealms = maxRealms;
    }

    /// <inheritdoc />
    public async ValueTask RegisterAsync(RealmInstance realm, CancellationToken cancellationToken = default)
    {
        var result = await ClaimAsync(realm, false, cancellationToken).ConfigureAwait(false);

        if (result != 1)
        {
            ThrowClaimFailure(result);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> RenewAsync(RealmInstance realm, CancellationToken cancellationToken = default)
    {
        var result = await ClaimAsync(realm, true, cancellationToken).ConfigureAwait(false);

        if (result is 1 or 2)
        {
            return true;
        }

        if (result is -1 or -4)
        {
            return false;
        }

        ThrowClaimFailure(result);

        return false;
    }

    /// <inheritdoc />
    public async ValueTask UnregisterAsync(RealmInstance realm, CancellationToken cancellationToken = default)
    {
        Validate(realm);
        await _redis.Connection
                    .GetDatabase()
                    .ScriptEvaluateAsync(
                        UnregisterScript,
                        [Key(realm.Descriptor.ServerIndex)],
                        [realm.Descriptor.RealmId, realm.InstanceId.ToString("N")]
                    )
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<RealmDescriptor>> GetAvailableAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        if (!Enum.IsDefined(accountType))
        {
            return [];
        }

        var server = _redis.Connection.GetServer(_redis.Connection.GetEndPoints()[0]);
        var realms = new List<RealmDescriptor>();

        await foreach (var key in server.KeysAsync(pattern: _prefix + "*", pageSize: 128)
                                        .WithCancellation(cancellationToken))
        {
            var instance = await ReadAsync(key, cancellationToken).ConfigureAwait(false);

            if (instance is not null && accountType >= instance.Descriptor.MinimumAccountType)
            {
                realms.Add(instance.Descriptor);
            }
        }

        return realms.OrderBy(realm => realm.ServerIndex).Take(_maxRealms).ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<RealmInstance?> FindByIndexAsync(
        ushort index,
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        if (!Enum.IsDefined(accountType))
        {
            return null;
        }

        var instance = await ReadAsync(Key(index), cancellationToken).ConfigureAwait(false);

        return instance is not null && accountType >= instance.Descriptor.MinimumAccountType ? instance : null;
    }

    private async ValueTask<int> ClaimAsync(
        RealmInstance realm,
        bool heartbeat,
        CancellationToken cancellationToken
    )
    {
        Validate(realm);
        var descriptor = realm.Descriptor;
        var result = await _redis.Connection
                                 .GetDatabase()
                                 .ScriptEvaluateAsync(
                                     ClaimScript,
                                     [Key(descriptor.ServerIndex)],
                                     [
                                         descriptor.RealmId,
                                         realm.InstanceId.ToString("N"),
                                         descriptor.ServerIndex.ToString(CultureInfo.InvariantCulture),
                                         descriptor.Name,
                                         descriptor.Address.ToString(),
                                         descriptor.Port.ToString(CultureInfo.InvariantCulture),
                                         ((int)descriptor.MinimumAccountType).ToString(CultureInfo.InvariantCulture),
                                         _prefix,
                                         _maxRealms,
                                         _leaseSeconds,
                                         heartbeat ? "heartbeat" : "claim"
                                     ]
                                 )
                                 .WaitAsync(cancellationToken)
                                 .ConfigureAwait(false);

        return (int)result;
    }

    private static void ThrowClaimFailure(int result)
    {
        switch (result)
        {
            case -1:
                throw new RealmDirectoryException(
                    RealmRegistrationError.DuplicateIndex,
                    "Realm server index is already registered."
                );
            case -2:
                throw new RealmDirectoryException(
                    RealmRegistrationError.CapacityExceeded,
                    "Realm directory capacity exceeded."
                );
            case -3:
                throw new RealmDirectoryException(
                    RealmRegistrationError.InvalidDescriptor,
                    "Realm metadata changed without a new instance ID."
                );
            default:
                throw new InvalidDataException("Redis returned an unknown realm registration result.");
        }
    }

    private RedisKey Key(ushort index)
    {
        return _prefix + index.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<RealmInstance?> ReadAsync(RedisKey key, CancellationToken cancellationToken)
    {
        HashEntry[] entries;

        try
        {
            entries = await _redis.Connection
                                  .GetDatabase()
                                  .HashGetAllAsync(key)
                                  .WaitAsync(cancellationToken)
                                  .ConfigureAwait(false);
        }
        catch (RedisServerException exception) when (exception.Message.StartsWith("WRONGTYPE", StringComparison.Ordinal))
        {
            return null;
        }

        if (entries.Length == 0)
        {
            return null;
        }

        var values = entries.ToDictionary(
            entry => (string)entry.Name!,
            entry => (string)entry.Value!,
            StringComparer.Ordinal
        );

        if (!values.TryGetValue("realm_id", out var id) ||
            !values.TryGetValue("instance_id", out var instanceText) ||
            !Guid.TryParseExact(instanceText, "N", out var instanceId) ||
            !values.TryGetValue("server_index", out var indexText) ||
            !ushort.TryParse(indexText, CultureInfo.InvariantCulture, out var index) ||
            !StringComparer.Ordinal.Equals((string)key, _prefix + index.ToString(CultureInfo.InvariantCulture)) ||
            !values.TryGetValue("name", out var name) ||
            !values.TryGetValue("ipv4", out var addressText) ||
            !IPAddress.TryParse(addressText, out var address) ||
            !values.TryGetValue("port", out var portText) ||
            !ushort.TryParse(portText, CultureInfo.InvariantCulture, out var port) ||
            !values.TryGetValue("minimum_account_type", out var minimumText) ||
            !int.TryParse(minimumText, CultureInfo.InvariantCulture, out var minimumNumber))
        {
            return null;
        }

        var descriptor = new RealmDescriptor(id, index, name, address, port, (AccountType)minimumNumber);

        try
        {
            Validate(new(descriptor, instanceId));

            return new(descriptor, instanceId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static void Validate(RealmInstance realm)
    {
        var descriptor = realm.Descriptor;

        if (realm.InstanceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(descriptor.RealmId) ||
            descriptor.RealmId.Length > 128 ||
            descriptor.Port == 0 ||
            descriptor.Address.AddressFamily != AddressFamily.InterNetwork ||
            descriptor.Address.Equals(IPAddress.Any) ||
            descriptor.Address.GetAddressBytes()[0] >= 224 ||
            !Enum.IsDefined(descriptor.MinimumAccountType))
        {
            throw new InvalidOperationException("Realm descriptor is invalid.");
        }

        _ = new GameServerEntry(descriptor.ServerIndex, descriptor.Name, 0, 0, descriptor.Address);
    }
}
