using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Integration.Server.Ultima.Accounts;

public sealed class AccountSerialTests
{
    [Fact]
    public async Task Migration_AlreadyAttachedDevelopmentSequence_IsPreserved()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        await fixture.Database.ExecuteAsync(
            "ALTER SEQUENCE auth.account_id_seq OWNED BY NONE; " +
            "CREATE SEQUENCE auth.accounts_id_seq AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 100 NO CYCLE; " +
            "ALTER SEQUENCE auth.accounts_id_seq OWNED BY auth.accounts.id;"
        );
        var migration = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "AccountMigrations", "0003_account_serial_ownership.sql")
        );
        await fixture.Database.ExecuteAsync(migration);
        var created = await fixture.Service.CreateAccountAsync("development", fixture.Password);
        Assert.True(created.Success, created.Exception?.ToString());
        Assert.Equal(100U, created.Account!.Id.Value);
        Assert.Equal("auth.accounts_id_seq", await fixture.Database.ScalarAsync<string>(
            "SELECT pg_get_serial_sequence('auth.accounts', 'id')"
        ));
    }

    [Fact]
    public async Task Migration_ExistingAccounts_ContinuesAboveTheirHighestIdWithoutResettingSequence()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        await fixture.SeedAsync();
        var sql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "AccountMigrations", "0001_account_id_sequence.sql")
        );
        await fixture.Database.ExecuteAsync(sql);
        var first = await fixture.Service.CreateAccountAsync("bob", fixture.Password);
        Assert.True(first.Success, first.Exception?.ToString());
        Assert.Equal(43U, first.Account!.Id.Value);
        await fixture.Database.ExecuteAsync(sql);
        var second = await fixture.Service.CreateAccountAsync("carol", fixture.Password);
        Assert.True(second.Success, second.Exception?.ToString());
        Assert.Equal(44U, second.Account!.Id.Value);
        Assert.Equal("alice", (await fixture.Accounts.GetByIdAsync(new(42)))!.Username);
    }

    [Fact]
    public async Task ReserveSerialAsync_ExhaustedSequence_FailsWithoutWrappingOrOverwriting()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        await fixture.Database.ExecuteAsync("SELECT setval('auth.account_id_seq', 4294967295, false)");
        var last = await fixture.Service.CreateAccountAsync("last", fixture.Password);
        Assert.True(last.Success, last.Exception?.ToString());
        Assert.Equal(uint.MaxValue, last.Account!.Id.Value);
        var exhausted = await fixture.Service.CreateAccountAsync("overflow", fixture.Password);
        Assert.False(exhausted.Success);
        Assert.NotNull(exhausted.Exception);
        Assert.Equal("last", Assert.Single(await fixture.Accounts.GetAllAsync()).Username);
    }

    [Theory, InlineData("world.account_id_seq"), InlineData("auth.account_id_seq; DROP TABLE auth.accounts"),
     InlineData("account_id_seq")]
    public async Task ReserveSerialAsync_InvalidSequence_DoesNotExecuteSqlOrConsumeIdentity(string sequence)
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Persistence.ReserveSerialAsync<AccountEntity>(sequence));
        var next = await fixture.Persistence.ReserveSerialAsync<AccountEntity>("auth.account_id_seq");
        Assert.Equal(1U, next.Value);
    }

    [Fact]
    public async Task ReserveSerialAsync_Canceled_DoesNotConsumeIdentity()
    {
        await using var fixture = await AccountServiceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Persistence.ReserveSerialAsync<AccountEntity>("auth.account_id_seq", cancellation.Token)
        );
        Assert.Equal(1U, (await fixture.Persistence.ReserveSerialAsync<AccountEntity>("auth.account_id_seq")).Value);
    }
}
