using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Nodes;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Integration.Realms;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit calls IAsyncLifetime.DisposeAsync after every test instance."
)]
public sealed class RedisGameHandoffStoreTests : IAsyncLifetime
{
    private readonly string _realmId = $"test-handoff-{Guid.NewGuid():N}";
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly List<uint> _issuedKeys = [];
    private RedisConnectionService _redis = null!;
    private HandoffProofService _proof = null!;
    private RedisGameHandoffStore _store = null!;

    public async Task InitializeAsync()
    {
        var endpoint = Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING") ??
                       throw new InvalidOperationException("MOONGATE_TEST_REDIS_CONNECTION_STRING is required.");
        _redis = new(
            new()
            {
                ConnectionString = endpoint,
                HandoffSecret = new('x', 32)
            }
        );
        await _redis.StartAsync();
        _proof = new(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        _store = new(_redis, _proof);
    }

    [Fact]
    public async Task IssueAsync_CreatesExpiringTicketWithValidRedirectKey()
    {
        var authKey = await IssueAsync();

        Assert.NotEqual(0u, authKey);
        Assert.NotEqual((byte)0xEF, (byte)(authKey >> 24));
        var ttl = await _redis.Connection.GetDatabase().KeyTimeToLiveAsync(Key(authKey));
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task RedeemAsync_ValidCredentialsReturnIdentityOnce()
    {
        var authKey = await IssueAsync();

        var first = await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password");
        var second = await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password");

        Assert.Equal(Handoff(), first);
        Assert.Null(second);
    }

    [Fact]
    public async Task RedeemAsync_InvalidCredentialsAndTargetDoNotConsumeTicket()
    {
        var authKey = await IssueAsync();

        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "wrong"));
        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, authKey, "alice", "password"));
        Assert.Null(await _store.RedeemAsync("wrong-realm", _instanceId, authKey, "Alice", "password"));
        Assert.Null(await _store.RedeemAsync(_realmId, Guid.NewGuid(), authKey, "Alice", "password"));
        Assert.Equal(Handoff(), await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password"));
    }

    [Theory, InlineData("AccountId", 43), InlineData("AccountType", 2)]
    public async Task RedeemAsync_TamperedIdentityDoesNotConsumeTicket(string field, int replacement)
    {
        var authKey = await IssueAsync();
        var database = _redis.Connection.GetDatabase();
        var raw = (string?)await database.StringGetAsync(Key(authKey));
        Assert.NotNull(raw);
        var ticket = JsonNode.Parse(raw);
        Assert.NotNull(ticket);
        ticket[field] = replacement;
        Assert.True(await database.StringSetAsync(Key(authKey), ticket.ToJsonString(), TimeSpan.FromSeconds(30)));

        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password"));
        Assert.True(await database.KeyExistsAsync(Key(authKey)));
    }

    [Fact]
    public async Task RedeemAsync_ConcurrentRequestsAdmitExactlyOne()
    {
        var authKey = await IssueAsync();

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password")
                    .AsTask()
                )
        );

        Assert.Single(attempts, result => result is not null);
    }

    [Fact]
    public async Task GameLoginPacket_RealRedisTicket_AssociatesIdentityAndRejectsReplay()
    {
        var authKey = await IssueAsync();
        await using var fixture = await SessionFixture.CreateAsync();
        var sessions = new SessionService(fixture.Loop);
        var session = sessions.GetOrCreate(fixture.Client);
        session.NetworkSession.SetSeed(authKey);
        var context = new PacketContext(session, fixture.Loop, sessions, new StubPacketSendService());
        var realm = new RealmInstance(
            new(
                _realmId,
                1,
                "Test Realm",
                IPAddress.Loopback,
                2595,
                AccountType.Regular
            ),
            _instanceId
        );
        var handler = new GameLoginPacketHandler(realm, _store, new StubDataLoaderService());

        await handler.HandleAsync(
            context,
            new(authKey, "Alice", "password"),
            CancellationToken.None
        );

        Assert.Equal(new(42), session.AccountId);
        Assert.Equal(AccountType.GameMaster, session.AccountType);
        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password"));
    }

    [Fact]
    public async Task IssueAsync_RetriesReservedKeysAndCollision()
    {
        var candidates = new Queue<uint>([0, 0xEF000001, 0x01020304, 0x01020304, 0x01020305]);
        var store = new RedisGameHandoffStore(_redis, _proof, () => candidates.Dequeue());
        var credentialKey = _proof.DeriveCredentialKey("Alice", "password");

        var first = await store.IssueAsync(Handoff(), credentialKey);
        var second = await store.IssueAsync(Handoff(), credentialKey);
        _issuedKeys.Add(first);
        _issuedKeys.Add(second);

        Assert.Equal(0x01020304u, first);
        Assert.Equal(0x01020305u, second);
    }

    [Fact]
    public async Task RedeemAsync_ExpiredOrMalformedTicketFailsClosed()
    {
        var expired = await IssueAsync();
        Assert.True(await _redis.Connection.GetDatabase().KeyExpireAsync(Key(expired), TimeSpan.FromMilliseconds(1)));
        await Task.Delay(50);
        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, expired, "Alice", "password"));

        var malformed = await IssueAsync();
        Assert.True(
            await _redis.Connection
                .GetDatabase()
                .StringSetAsync(
                    Key(malformed),
                    "not-a-ticket",
                    TimeSpan.FromSeconds(30)
                )
        );
        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, malformed, "Alice", "password"));
    }

    [Fact]
    public async Task RevokeAsync_RemovesUnsentRedirectTicket()
    {
        var authKey = await IssueAsync();

        await _store.RevokeAsync(_realmId, authKey);

        Assert.Null(await _store.RedeemAsync(_realmId, _instanceId, authKey, "Alice", "password"));
    }

    private PendingHandoff Handoff()
    {
        return new(new(42), AccountType.GameMaster, "Alice", _realmId, _instanceId, "7.0.117");
    }

    private async Task<uint> IssueAsync()
    {
        var credentialKey = _proof.DeriveCredentialKey("Alice", "password");
        var key = await _store.IssueAsync(Handoff(), credentialKey);
        _issuedKeys.Add(key);

        return key;
    }

    private string Key(uint authKey)
    {
        return $"moongate:handoff:{_realmId}:{authKey:X8}";
    }

    public async Task DisposeAsync()
    {
        foreach (var key in _issuedKeys)
        {
            await _redis.Connection.GetDatabase().KeyDeleteAsync(Key(key));
        }

        _proof.Dispose();
        await _redis.DisposeAsync();
    }
}
