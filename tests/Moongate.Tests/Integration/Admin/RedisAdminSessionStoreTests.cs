using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Exceptions.Admin;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Tests.TestSupport.Admin;

namespace Moongate.Tests.Integration.Admin;

public sealed class RedisAdminSessionStoreTests
{
    private static AdminIdentity Identity => new(new(1), "admin", AccountType.Administrator);

    [Fact]
    public async Task IssueAsync_ExpiredIndexEntries_ArePrunedAndLifetimeIsAbsolute()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        var digest = AdminRedisFixture.Digest();
        await fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMilliseconds(80));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (await fixture.Store.FindAsync(digest) is not null)
        {
            await Task.Delay(10, deadline.Token);
        }
        var database = fixture.Redis.Connection.GetDatabase();
        await database.SortedSetAddAsync(fixture.Prefix + "index:1", fixture.Prefix + "session:expired", 1);
        await fixture.Store.IssueAsync(Identity, gate.Generation, AdminRedisFixture.Digest(), TimeSpan.FromMinutes(1));
        Assert.Equal(1, await database.SortedSetLengthAsync(fixture.Prefix + "index:1"));
    }

    [Fact]
    public async Task Operations_DisconnectedDependency_FailClosed()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        await fixture.Redis.StopAsync();

        try
        {
            await Assert.ThrowsAsync<AdminDependencyUnavailableException>(
                () => fixture.Store.FindAsync(AdminRedisFixture.Digest())
            );
            await Assert.ThrowsAsync<AdminDependencyUnavailableException>(
                () => fixture.Throttle.TryAcquireAsync("127.0.0.1", "alice")
            );
        }
        finally
        {
            await fixture.Redis.StartAsync();
        }
    }

    [Fact]
    public async Task IssueAsync_InvalidRoleOrCanceled_DoesNotCreateSession()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        var digest = AdminRedisFixture.Digest();
        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Store.IssueAsync(
                new(new(1), "admin", (AccountType)99),
                gate.Generation,
                digest,
                TimeSpan.FromMinutes(1)
            )
        );
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1), cancellation.Token)
        );
        Assert.Null(await fixture.Store.FindAsync(digest));
    }

    [Fact]
    public async Task FindAsync_GenerationChanged_RejectsExistingSession()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        var digest = AdminRedisFixture.Digest();
        var issued = await fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1));
        Assert.Equal("admin", (await fixture.Store.FindAsync(digest))!.Identity.Username);
        Assert.InRange(issued.ExpiresAt, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        Assert.Null(await fixture.Store.FindAsync(digest));
        Assert.False(await fixture.Redis.Connection.GetDatabase().KeyExistsAsync(fixture.Prefix + "session:" + digest));
    }

    [Fact]
    public async Task IssueAsync_BlockedStaleOrDuplicate_RejectsWithoutReopening()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, true);
        var digest = AdminRedisFixture.Digest();
        await Assert.ThrowsAsync<AdminSessionRejectedException>(
            () => fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1))
        );
        Assert.Null(await fixture.Redis.Connection.GetDatabase().KeyTimeToLiveAsync(fixture.Prefix + "gate:1"));
        Assert.True(await fixture.Store.TryOpenGateAsync(Identity.AccountId, gate.Generation));
        await fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<AdminSessionRejectedException>(
            () => fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1))
        );
        var next = await fixture.Store.ResetGateAsync(Identity.AccountId, true);
        Assert.NotEqual(gate.Generation, next.Generation);
        Assert.False(await fixture.Store.TryOpenGateAsync(Identity.AccountId, gate.Generation));
        Assert.True((await fixture.Store.ReadGateAsync(Identity.AccountId))!.Blocked);
    }

    [Theory, InlineData("missing-gate"), InlineData("expired"), InlineData("unknown-role"), InlineData("malformed")]
    public async Task FindAsync_InvalidRecord_FailsClosed(string kind)
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        var digest = AdminRedisFixture.Digest();
        await fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1));
        var database = fixture.Redis.Connection.GetDatabase();
        var key = fixture.Prefix + "session:" + digest;

        switch (kind)
        {
            case "missing-gate":
                await database.KeyDeleteAsync(fixture.Prefix + "gate:1");

                break;
            case "expired":
                await database.HashSetAsync(key, "expires", 1);

                break;
            case "unknown-role":
                await database.HashSetAsync(key, "role", 99);

                break;
            default:
                await database.HashDeleteAsync(key, "username");

                break;
        }
        Assert.Null(await fixture.Store.FindAsync(digest));
    }

    [Fact]
    public async Task IssueAsync_SessionLimitAndLogout_KeepBoundedIndex()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        var digests = Enumerable.Range(0, 64).Select(_ => AdminRedisFixture.Digest()).ToArray();

        foreach (var digest in digests)
        {
            await fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1));
        }
        await Assert.ThrowsAsync<AdminSessionLimitException>(
            () => fixture.Store.IssueAsync(Identity, gate.Generation, AdminRedisFixture.Digest(), TimeSpan.FromMinutes(1))
        );
        await fixture.Store.RemoveAsync(digests[0]);
        await fixture.Store.RemoveAsync(digests[0]);
        Assert.Null(await fixture.Store.FindAsync(digests[0]));
        await fixture.Store.IssueAsync(Identity, gate.Generation, AdminRedisFixture.Digest(), TimeSpan.FromMinutes(1));
        var database = fixture.Redis.Connection.GetDatabase();
        Assert.Equal(64, await database.SortedSetLengthAsync(fixture.Prefix + "index:1"));
        Assert.NotNull(await database.KeyTimeToLiveAsync(fixture.Prefix + "index:1"));
        await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        Assert.False(await database.KeyExistsAsync(fixture.Prefix + "index:1"));
    }

    [Fact]
    public async Task IssueAsync_ConcurrentReset_NeverAcceptsOldGeneration()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var gate = await fixture.Store.ResetGateAsync(Identity.AccountId, false);
        var digest = AdminRedisFixture.Digest();
        var issue = fixture.Store.IssueAsync(Identity, gate.Generation, digest, TimeSpan.FromMinutes(1));
        await fixture.Store.ResetGateAsync(Identity.AccountId, false);

        try { await issue; }
        catch (AdminSessionRejectedException) { }
        Assert.Null(await fixture.Store.FindAsync(digest));
    }

    [Fact]
    public async Task FindAsync_WrongRedisType_ReportsDependencyFailure()
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var digest = AdminRedisFixture.Digest();
        await fixture.Redis.Connection.GetDatabase().StringSetAsync(fixture.Prefix + "session:" + digest, "bad");
        await Assert.ThrowsAsync<AdminDependencyUnavailableException>(() => fixture.Store.FindAsync(digest));
    }
}
