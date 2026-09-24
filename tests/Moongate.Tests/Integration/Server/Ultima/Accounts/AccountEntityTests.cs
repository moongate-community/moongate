using Moongate.Core.Utils;
using Npgsql;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Integration.Server.Ultima.Accounts;

[Collection(PostgresTestCollection.Name)]
public sealed class AccountEntityTests
{
    [Fact]
    public async Task Migration_ExistingApiAccess_PreservesExplicitGrantAndSetsDefaultFalse()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        await fixture.Database.ExecuteAsync("UPDATE auth.accounts SET can_access_api=true; ALTER TABLE auth.accounts ALTER COLUMN can_access_api SET DEFAULT true;");
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "AccountMigrations", "0004_account_admin_api_access.sql"));
        await fixture.Database.ExecuteAsync(sql);
        Assert.True((await fixture.Accounts.GetByIdAsync(new(42)))!.CanAccessApi);
        Assert.Equal("false", await fixture.Database.ScalarAsync<string>("SELECT column_default FROM information_schema.columns WHERE table_schema='auth' AND table_name='accounts' AND column_name='can_access_api'"));
    }

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
        Assert.False(stored.CanAccessApi);
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
