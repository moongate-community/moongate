using Moongate.Core.Types;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Accounts;

public class AccountServiceGetAllTests
{
    private static AccountService Build()
    {
        var persistence = new FakePersistenceService();

        return new(
            persistence,
            CharacterServiceFixture.Create(persistence, new EventBusService()),
            new StubSessionManager()
        );
    }

    [Fact]
    public void GetAll_ReturnsEveryAccount()
    {
        var accounts = Build();
        accounts.Create("tom", "secret", null, AccountLevelType.Administrator);
        accounts.Create("alice", "secret", "a@b.c", AccountLevelType.Player);

        var all = accounts.GetAll();

        Assert.Equal(["alice", "tom"], all.Select(account => account.Username).OrderBy(name => name));
    }

    [Fact]
    public void GetAll_NoAccounts_IsEmpty()
        => Assert.Empty(Build().GetAll());
}
