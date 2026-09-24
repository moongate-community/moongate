using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Internal.Realms;
using Moongate.Server.Services.Redis;
using StackExchange.Redis;

namespace Moongate.Server.Services.Realms;

/// <summary>Stores short-lived redirect tickets in the shared Redis instance.</summary>
public sealed class RedisGameHandoffStore : IGameHandoffStore
{
    private const int MaxIssueAttempts = 32;
    private const int ProofSize = 32;
    private const byte VersionSeedOpcode = 0xEF;
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(30);

    private readonly RedisConnectionService _redis;
    private readonly IHandoffProofService _proof;
    private readonly Func<uint> _generateAuthKey;

    public RedisGameHandoffStore(RedisConnectionService redis, IHandoffProofService proof)
        : this(redis, proof, GenerateAuthKey)
    {
    }

    internal RedisGameHandoffStore(RedisConnectionService redis, IHandoffProofService proof,
        Func<uint> generateAuthKey)
    {
        _redis = redis;
        _proof = proof;
        _generateAuthKey = generateAuthKey;
    }

    public async ValueTask<uint> IssueAsync(PendingHandoff handoff, ReadOnlyMemory<byte> credentialKey,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(handoff);

        if (!handoff.AccountId.IsValid || !Enum.IsDefined(handoff.AccountType) ||
            string.IsNullOrEmpty(handoff.Username) || string.IsNullOrEmpty(handoff.RealmId) ||
            handoff.InstanceId == Guid.Empty)
        {
            throw new ArgumentException("The pending handoff must identify an account and realm instance.",
                nameof(handoff));
        }

        var database = _redis.Connection.GetDatabase();

        for (var attempt = 0; attempt < MaxIssueAttempts; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var authKey = _generateAuthKey();

            if (authKey == 0 || (byte)(authKey >> 24) == VersionSeedOpcode)
            {
                continue;
            }

            var proof = _proof.Sign(credentialKey.Span, handoff, authKey);

            try
            {
                var ticket = new RedisHandoffTicket(handoff.AccountId.Value, handoff.AccountType,
                    handoff.Username, handoff.RealmId, handoff.InstanceId, handoff.ClientVersion, proof);
                var encoded = JsonSerializer.SerializeToUtf8Bytes(ticket);

                try
                {
                    var key = Key(handoff.RealmId, authKey);
                    var issued = await database.StringSetAsync(key, encoded, TicketLifetime, When.NotExists)
                                               .ConfigureAwait(false);

                    if (token.IsCancellationRequested)
                    {
                        if (issued)
                        {
                            await database.KeyDeleteAsync(key).ConfigureAwait(false);
                        }

                        token.ThrowIfCancellationRequested();
                    }

                    if (issued)
                    {
                        return authKey;
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(encoded);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(proof);
            }
        }

        throw new InvalidOperationException("Unable to issue a unique game redirect key.");
    }

    public async ValueTask<PendingHandoff?> RedeemAsync(string realmId, Guid instanceId, uint authKey,
        string username, string password, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(realmId) || string.IsNullOrEmpty(username) ||
            string.IsNullOrEmpty(password) || instanceId == Guid.Empty || authKey == 0)
        {
            return null;
        }

        token.ThrowIfCancellationRequested();
        var database = _redis.Connection.GetDatabase();
        var key = Key(realmId, authKey);
        var candidate = await database.StringGetAsync(key).ConfigureAwait(false);

        if (candidate.IsNull)
        {
            return null;
        }

        var candidateBytes = (byte[]?)candidate;

        if (candidateBytes is null)
        {
            return null;
        }

        try
        {
            if (!TryReadTicket(candidateBytes, out var ticket))
            {
                return null;
            }

            try
            {
                var handoff = new PendingHandoff(new Serial(ticket.AccountId), ticket.AccountType,
                    ticket.Username, ticket.RealmId, ticket.InstanceId, ticket.ClientVersion);

                if (!StringComparer.Ordinal.Equals(ticket.RealmId, realmId) ||
                    !StringComparer.Ordinal.Equals(ticket.Username, username) ||
                    ticket.InstanceId != instanceId ||
                    !_proof.Verify(username, password, handoff, authKey, ticket.Proof))
                {
                    return null;
                }

                token.ThrowIfCancellationRequested();
                var consumed = await database.StringGetDeleteAsync(key).ConfigureAwait(false);
                var consumedBytes = (byte[]?)consumed;

                try
                {
                    return consumedBytes is not null &&
                           CryptographicOperations.FixedTimeEquals(candidateBytes, consumedBytes)
                               ? handoff
                               : null;
                }
                finally
                {
                    if (consumedBytes is not null)
                    {
                        CryptographicOperations.ZeroMemory(consumedBytes);
                    }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ticket.Proof);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidateBytes);
        }
    }

    public async ValueTask RevokeAsync(string realmId, uint authKey, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(realmId);
        token.ThrowIfCancellationRequested();
        await _redis.Connection.GetDatabase().KeyDeleteAsync(Key(realmId, authKey)).ConfigureAwait(false);
    }

    private static uint GenerateAuthKey()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static RedisKey Key(string realmId, uint authKey)
        => $"moongate:handoff:{realmId}:{authKey:X8}";

    private static bool TryReadTicket(byte[] encoded, [NotNullWhen(true)] out RedisHandoffTicket? ticket)
    {
        ticket = null;

        try
        {
            var parsed = JsonSerializer.Deserialize<RedisHandoffTicket>(encoded);

            if (parsed is null || parsed.AccountId == 0 || !Enum.IsDefined(parsed.AccountType) ||
                string.IsNullOrEmpty(parsed.Username) || string.IsNullOrEmpty(parsed.RealmId) ||
                parsed.InstanceId == Guid.Empty || parsed.Proof is not { Length: ProofSize })
            {
                if (parsed?.Proof is { } invalidProof)
                {
                    CryptographicOperations.ZeroMemory(invalidProof);
                }

                return false;
            }

            ticket = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
