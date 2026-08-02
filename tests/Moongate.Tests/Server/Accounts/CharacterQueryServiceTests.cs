using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.Accounts;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.Accounts;

/// <summary>
/// Nothing on a mobile says it is a player character — the only link is the owning account's id list.
/// That rule is this service's whole reason to exist, and every case here is about it.
/// </summary>
public class CharacterQueryServiceTests
{
    [Fact]
    public void Find_ReturnsTheCharacterAndItsOwner()
    {
        var persistence = new FakePersistenceService();
        var character = Mobile(1, "Squid");

        persistence.Store<MobileEntity>().UpsertAsync(character);

        var service = new CharacterQueryService(Owning("tom", character.Id), persistence);

        var found = service.Find(character.Id);

        Assert.NotNull(found);
        Assert.Equal("tom", found.AccountUsername);
        Assert.Equal(character.Id, found.Mobile.Id);
    }

    // An NPC addressable by serial would turn a route meant for characters into a window onto every
    // mobile on the shard.
    [Fact]
    public void Find_IgnoresAMobileNoAccountOwns()
    {
        var persistence = new FakePersistenceService();
        var npc = Mobile(2, "a wandering healer");

        persistence.Store<MobileEntity>().UpsertAsync(npc);

        var service = new CharacterQueryService(Owning("tom"), persistence);

        Assert.Null(service.Find(npc.Id));
    }

    [Fact]
    public void Find_ReturnsNullForAnUnknownSerial()
    {
        var service = new CharacterQueryService(Owning("tom"), new FakePersistenceService());

        Assert.Null(service.Find(new(0xDEAD)));
    }

    private static MobileEntity Mobile(uint serial, string name)
        => new() { Id = new(serial), Name = name };

    private static IAccountService Owning(string username, params Serial[] characters)
        => new AccountsStub(username, characters);

    /// <summary>
    /// One account owning the given characters. Only <see cref="GetAll" /> is real — it is the single
    /// thing the service under test asks for, and everything else throwing is what makes a test that
    /// wandered off say so.
    /// </summary>
    private sealed class AccountsStub : IAccountService
    {
        private readonly AccountEntity _account;

        public AccountsStub(string username, IEnumerable<Serial> characters)
        {
            _account = new()
            {
                Id = new(1),
                Username = username,
                AccountLevel = AccountLevelType.Player,
                IsActive = true,
                MobileIds = [.. characters],
            };
        }

        public IReadOnlyList<AccountEntity> GetAll()
            => [_account];

        public AccountAuthResult Authenticate(string username, string password)
            => throw new NotSupportedException();

        public AccountCreateResultType Create(string username, string password, string? email, AccountLevelType level)
            => throw new NotSupportedException();

        public AccountDeleteResultType Delete(string username)
            => throw new NotSupportedException();

        public Serial? GetAccountIdByUsername(string username)
            => throw new NotSupportedException();

        public AccountEntity? GetById(Serial accountId)
            => throw new NotSupportedException();

        public AccountEntity? GetByUsername(string username)
            => throw new NotSupportedException();

        public IReadOnlyList<string> GetUsernames()
            => throw new NotSupportedException();

        public AccountRegisterResult RegisterPending(string username, string password, string email)
            => throw new NotSupportedException();

        public AccountResendResultType ResendVerification(string username, string email)
            => throw new NotSupportedException();

        public bool SetActive(string username, bool isActive)
            => throw new NotSupportedException();

        public bool SetLevel(string username, AccountLevelType level)
            => throw new NotSupportedException();

        public bool SetPassword(string username, string password)
            => throw new NotSupportedException();

        public AccountVerifyResultType VerifyEmail(string token)
            => throw new NotSupportedException();
    }
}
