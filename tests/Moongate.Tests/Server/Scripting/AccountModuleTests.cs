using Moongate.Core.Types;
using Moongate.Server.Scripting;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Scripting;

public class AccountModuleTests
{
    [Fact]
    public void Create_LevelAcceptsTheNumericEnumValue()
    {
        var (module, accounts) = Build();

        Assert.True(module.Create("tom", "secret", null, (int)AccountLevelType.GrandMaster));

        Assert.Equal(AccountLevelType.GrandMaster, accounts.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void Create_MakesAnAccountThatCanLogIn()
    {
        var (module, accounts) = Build();

        Assert.True(module.Create("tom", "secret", "tom@example.com", "Administrator"));

        Assert.True(accounts.Authenticate("tom", "secret").Success);
        Assert.Equal(AccountLevelType.Administrator, accounts.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void Create_UnknownLevel_FallsBackToPlayerRatherThanRefusing()
    {
        var (module, accounts) = Build();

        Assert.True(module.Create("tom", "secret", null, "Overlord"));

        Assert.Equal(AccountLevelType.Player, accounts.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void Create_UsernameTaken_ReturnsFalse()
    {
        var (module, _) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.False(module.Create("tom", "other", null, "Player"));
    }

    [Fact]
    public void Delete_RemovesTheAccount()
    {
        var (module, accounts) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.True(module.Delete("tom"));
        Assert.Null(accounts.GetByUsername("tom"));
    }

    [Fact]
    public void Delete_UnknownUsername_ReturnsFalse()
        => Assert.False(Build().Module.Delete("nobody"));

    [Fact]
    public void Exists_IsTrueOnlyForAKnownUsername()
    {
        var (module, _) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.True(module.Exists("tom"));
        Assert.False(module.Exists("nobody"));
    }

    [Fact]
    public void Get_NeverHandsThePasswordHashToScripts()
    {
        var (module, _) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.DoesNotContain(
            module.Get("tom")!.ToDictionary().Keys,
            key => key.Contains("password", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Get_ReturnsTheAccountsFieldTable()
    {
        var (module, accounts) = Build();
        module.Create("tom", "secret", "tom@example.com", "GrandMaster");

        var table = module.Get("tom")?.ToDictionary();

        Assert.NotNull(table);
        Assert.Equal("tom", table!["username"]);
        Assert.Equal("tom@example.com", table["email"]);
        Assert.Equal("GrandMaster", table["level"]);
        Assert.Equal(true, table["is_active"]);
        Assert.Equal(accounts.GetByUsername("tom")!.Id.Value, table["id"]);
        Assert.Empty((List<uint>)table["mobiles"]!);
    }

    [Fact]
    public void Get_UnknownUsername_ReturnsNil()
        => Assert.Null(Build().Module.Get("nobody"));

    [Fact]
    public void List_ReturnsEveryUsername()
    {
        var (module, _) = Build();
        module.Create("tom", "a", null, "Player");
        module.Create("ada", "b", null, "Player");

        Assert.Equal(new[] { "tom", "ada" }, module.List());
    }

    [Fact]
    public void Mutators_UnknownUsername_ReturnFalse()
    {
        var (module, _) = Build();

        Assert.False(module.SetPassword("nobody", "secret"));
        Assert.False(module.SetLevel("nobody", "Player"));
        Assert.False(module.SetActive("nobody", false));
    }

    [Fact]
    public void SetActive_BlocksAndUnblocksLogin()
    {
        var (module, accounts) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.True(module.SetActive("tom", false));
        Assert.False(accounts.Authenticate("tom", "secret").Success);

        Assert.True(module.SetActive("tom", true));
        Assert.True(accounts.Authenticate("tom", "secret").Success);
    }

    [Fact]
    public void SetLevel_AcceptsTheEnumNameAndChangesTheLevel()
    {
        var (module, accounts) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.True(module.SetLevel("tom", "Administrator"));
        Assert.Equal(AccountLevelType.Administrator, accounts.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void SetLevel_UnknownLevel_ReturnsFalseAndLeavesTheLevelAlone()
    {
        var (module, accounts) = Build();
        module.Create("tom", "secret", null, "Player");

        Assert.False(module.SetLevel("tom", "Overlord"));
        Assert.Equal(AccountLevelType.Player, accounts.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void SetPassword_SwapsTheLoginPassword()
    {
        var (module, accounts) = Build();
        module.Create("tom", "old", null, "Player");

        Assert.True(module.SetPassword("tom", "new"));

        Assert.False(accounts.Authenticate("tom", "old").Success);
        Assert.True(accounts.Authenticate("tom", "new").Success);
    }

    private static (AccountModule Module, AccountService Accounts) Build()
    {
        var persistence = new FakePersistenceService();
        var bus = new EventBusService();
        var accounts = new AccountService(
            persistence,
            CharacterServiceFixture.Create(persistence, bus),
            new StubSessionManager(),
            bus
        );

        return (new(accounts, new StubLoopThread()), accounts);
    }
}
