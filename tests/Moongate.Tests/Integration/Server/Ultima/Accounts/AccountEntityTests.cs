using Moongate.Core.Utils;
using Npgsql;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Integration.Server.Ultima.Accounts;

public sealed class AccountEntityTests
{
    [Fact]
    public async Task Persistence_RoundTripsAllAccountFieldsInAuthDatabase()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var account = await fixture.SeedAsync(locked: true);
        var stored = await fixture.Accounts.GetByIdAsync(account.Id);
        Assert.NotNull(stored);
        Assert.NotSame(account, stored);
        Assert.Equal(account.Id, stored.Id);
        Assert.Equal("alice", stored.Username);
        Assert.Equal("alice@example.invalid", stored.Email);
        Assert.Equal(account.AccountType, stored.AccountType);
        Assert.Equal(account.CreatedAt, stored.CreatedAt);
        Assert.Equal(account.UpdatedAt, stored.UpdatedAt);
        Assert.Null(stored.LastLoginAt);
        Assert.True(stored.IsLocked);
        Assert.Equal(account.HashPassword, stored.HashPassword);
        Assert.True(HashUtils.VerifyPassword(fixture.Password, stored.HashPassword));
        Assert.Equal(42L, await fixture.Database.ScalarAsync<long>("SELECT id FROM auth.accounts"));
        Assert.False(await fixture.WorldDatabase.ScalarAsync<bool>("SELECT to_regclass('auth.accounts') IS NOT NULL"));
    }

    [Fact]
    public async Task Persistence_DuplicateUsernameWithDifferentIdentity_IsRejected()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        var duplicate = await fixture.SeedAsync();
        duplicate.Id = new(43);
        var exception = await Record.ExceptionAsync(() => fixture.Accounts.UpsertAsync(duplicate));
        Assert.NotNull(exception);
        var postgres = Assert.IsType<PostgresException>(exception.GetBaseException());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Single(await fixture.Accounts.GetAllAsync());
    }
}
