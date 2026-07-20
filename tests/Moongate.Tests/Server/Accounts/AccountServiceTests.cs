using Moongate.Core.Types;
using Moongate.Network.Packets.Incoming;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using SquidStd.Core.Utils;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.Accounts;

public class AccountServiceTests
{
    [Theory, InlineData(""), InlineData("   ")]
    public void Create_BlankUsername_IsRefused(string username)
        => Assert.Equal(
            AccountCreateResultType.UsernameEmpty,
            Service(new()).Create(username, "secret", null, AccountLevelType.Player)
        );

    [Fact]
    public void Create_EmptyPassword_IsRefused()
        => Assert.Equal(
            AccountCreateResultType.PasswordEmpty,
            Service(new()).Create("tom", string.Empty, null, AccountLevelType.Player)
        );

    [Fact]
    public void Create_PersistsAnActiveAccountWithAHashedPassword()
    {
        var persistence = new FakePersistenceService();
        var service = Service(persistence);

        var result = service.Create("tom", "secret", "tom@example.com", AccountLevelType.Administrator);

        Assert.Equal(AccountCreateResultType.Created, result);

        var account = service.GetByUsername("tom");
        Assert.NotNull(account);
        Assert.Equal("tom@example.com", account!.Email);
        Assert.Equal(AccountLevelType.Administrator, account.AccountLevel);
        Assert.True(account.IsActive);

        // The password is stored hashed, never in the clear, and the hash verifies.
        Assert.NotEqual("secret", account.PasswordHash);
        Assert.True(HashUtils.VerifyPassword("secret", account.PasswordHash));
    }

    [Fact]
    public void Create_TheAccountCanThenLogIn()
    {
        var service = Service(new());
        service.Create("tom", "secret", null, AccountLevelType.Player);

        Assert.True(service.Authenticate("tom", "secret").Success);
    }

    [Fact]
    public void Create_UsernameAlreadyTaken_IsRefusedAndLeavesTheFirstAccountAlone()
    {
        var service = Service(new());
        service.Create("tom", "secret", null, AccountLevelType.Player);

        var result = service.Create("tom", "other", null, AccountLevelType.Administrator);

        Assert.Equal(AccountCreateResultType.UsernameTaken, result);
        Assert.True(service.Authenticate("tom", "secret").Success);
        Assert.Equal(AccountLevelType.Player, service.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void Delete_AccountWithNoCharacters_StillGoes()
    {
        var service = Service(new());
        service.Create("tom", "secret", null, AccountLevelType.Player);

        Assert.Equal(AccountDeleteResultType.Deleted, service.Delete("tom"));
        Assert.Null(service.GetByUsername("tom"));
    }

    [Fact]
    public void Delete_AccountWithSeveralCharacters_TakesThemAll()
    {
        var persistence = new FakePersistenceService();
        var characters = CharacterServiceFixture.Create(persistence, new EventBusService());
        var service = Service(persistence, characters);

        service.Create("tom", "secret", null, AccountLevelType.Player);
        var accountId = service.GetByUsername("tom")!.Id;
        var first = characters.CreateCharacter(accountId, Packet());
        var second = characters.CreateCharacter(accountId, Packet());

        Assert.Equal(AccountDeleteResultType.Deleted, service.Delete("tom"));

        Assert.Null(persistence.Store<MobileEntity>().GetById(first.Id));
        Assert.Null(persistence.Store<MobileEntity>().GetById(second.Id));
    }

    [Fact]
    public void Delete_ACharacterIsBeingPlayed_IsRefusedAndNothingIsDeleted()
    {
        var persistence = new FakePersistenceService();
        var sessions = new StubSessionManager();
        var characters = CharacterServiceFixture.Create(persistence, new EventBusService(), sessions);
        var service = Service(persistence, characters, sessions);

        service.Create("tom", "secret", null, AccountLevelType.Player);
        var accountId = service.GetByUsername("tom")!.Id;
        var played = characters.CreateCharacter(accountId, Packet());
        var other = characters.CreateCharacter(accountId, Packet());
        sessions.Played.Add(played.Id);

        Assert.Equal(AccountDeleteResultType.CharacterBeingPlayed, service.Delete("tom"));

        // Refused as a whole: the account keeps every character, not just the one in play.
        Assert.NotNull(service.GetByUsername("tom"));
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(played.Id));
        Assert.NotNull(persistence.Store<MobileEntity>().GetById(other.Id));
    }

    [Fact]
    public void Delete_RemovesTheAccountItsCharactersAndWhatTheyCarry()
    {
        var persistence = new FakePersistenceService();
        var characters = CharacterServiceFixture.Create(persistence, new EventBusService());
        var service = Service(persistence, characters);

        service.Create("tom", "secret", null, AccountLevelType.Player);
        var accountId = service.GetByUsername("tom")!.Id;
        var mobile = characters.CreateCharacter(accountId, Packet());
        var backpackId = mobile.BackpackId;

        Assert.Equal(AccountDeleteResultType.Deleted, service.Delete("tom"));

        Assert.Null(service.GetByUsername("tom"));
        Assert.Null(persistence.Store<MobileEntity>().GetById(mobile.Id));

        // The cascade reaches past the character into what it was carrying.
        Assert.Null(persistence.Store<ItemEntity>().GetById(backpackId));
    }

    [Fact]
    public void Delete_UnknownUsername_ReturnsNotFound()
        => Assert.Equal(AccountDeleteResultType.NotFound, Service(new()).Delete("nobody"));

    [Fact]
    public void GetById_KnownAccount_ReturnsIt()
    {
        // The JWT carries the account id in Sub, so a route holding a token can look the account up
        // directly. GetByUsername answers the same question by scanning and deep-cloning every account.
        var persistence = new FakePersistenceService();
        var service = Service(persistence);
        service.Create("tom", "secret", null, AccountLevelType.Player);
        var created = service.GetByUsername("tom");

        var fetched = service.GetById(created!.Id);

        Assert.NotNull(fetched);
        Assert.Equal("tom", fetched!.Username);
    }

    [Fact]
    public void GetById_UnknownAccount_ReturnsNull()
        => Assert.Null(Service(new()).GetById(new(0xDEADBEEF)));

    [Fact]
    public void GetByUsername_UnknownUsername_ReturnsNull()
        => Assert.Null(Service(new()).GetByUsername("nobody"));

    [Fact]
    public void GetUsernames_ReturnsEveryAccount()
    {
        var service = Service(new());
        service.Create("tom", "a", null, AccountLevelType.Player);
        service.Create("ada", "b", null, AccountLevelType.Player);

        Assert.Equal(new[] { "tom", "ada" }, service.GetUsernames());
    }

    [Fact]
    public void SetActive_BackToTrue_LetsTheAccountLogInAgain()
    {
        var service = Service(new());
        service.Create("tom", "secret", null, AccountLevelType.Player);
        service.SetActive("tom", false);

        Assert.True(service.SetActive("tom", true));
        Assert.True(service.Authenticate("tom", "secret").Success);
    }

    [Fact]
    public void SetActive_False_BlocksLoginWithTheRightPassword()
    {
        var service = Service(new());
        service.Create("tom", "secret", null, AccountLevelType.Player);

        Assert.True(service.SetActive("tom", false));

        var result = service.Authenticate("tom", "secret");
        Assert.False(result.Success);
        Assert.Equal(LoginDeniedReasonType.AccountBlocked, result.Reason);
    }

    [Fact]
    public void SetActive_UnknownUsername_ReturnsFalse()
        => Assert.False(Service(new()).SetActive("nobody", false));

    [Fact]
    public void SetLevel_ChangesTheLevel()
    {
        var service = Service(new());
        service.Create("tom", "secret", null, AccountLevelType.Player);

        Assert.True(service.SetLevel("tom", AccountLevelType.GrandMaster));
        Assert.Equal(AccountLevelType.GrandMaster, service.GetByUsername("tom")!.AccountLevel);
    }

    [Fact]
    public void SetLevel_UnknownUsername_ReturnsFalse()
        => Assert.False(Service(new()).SetLevel("nobody", AccountLevelType.Administrator));

    [Fact]
    public void SetPassword_EmptyPassword_IsRefusedAndLeavesTheOldOneWorking()
    {
        var service = Service(new());
        service.Create("tom", "old", null, AccountLevelType.Player);

        Assert.False(service.SetPassword("tom", string.Empty));
        Assert.True(service.Authenticate("tom", "old").Success);
    }

    [Fact]
    public void SetPassword_TheOldPasswordStopsWorkingAndTheNewOneStarts()
    {
        var service = Service(new());
        service.Create("tom", "old", null, AccountLevelType.Player);

        Assert.True(service.SetPassword("tom", "new"));

        Assert.False(service.Authenticate("tom", "old").Success);
        Assert.True(service.Authenticate("tom", "new").Success);
    }

    [Fact]
    public void SetPassword_UnknownUsername_ReturnsFalse()
        => Assert.False(Service(new()).SetPassword("nobody", "secret"));

    private static CharacterCreationPacket Packet()
        => new(
            0,
            "Freydis",
            0,
            4,
            GenderType.Female,
            RaceType.Elf,
            45,
            20,
            25,
            [new(1, 50)],
            0x03EA,
            0x203C,
            0x044E,
            0x2040,
            0x0450,
            0,
            0x0765,
            0x0766
        );

    private static AccountService Service(
        FakePersistenceService persistence,
        ICharacterService? characters = null,
        ISessionManager? sessions = null
    )
    {
        var bus = new EventBusService();

        return new(
            persistence,
            characters ?? CharacterServiceFixture.Create(persistence, bus),
            sessions ?? new StubSessionManager(),
            bus
        );
    }
}
