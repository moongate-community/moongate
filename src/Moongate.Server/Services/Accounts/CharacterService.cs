using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Interfaces;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Accounts;

public class CharacterService : ICharacterService
{
    private readonly IEntityStore<MobileEntity, Serial> _mobileStore;
    private readonly IEntityStore<AccountEntity, Serial> _accountStore;

    public CharacterService(IEntityStore<MobileEntity, Serial> mobileStore, IEntityStore<AccountEntity, Serial> accountStore)
    {
        _mobileStore = mobileStore;
        _accountStore = accountStore;
    }

    public IReadOnlyCollection<MobileEntity> GetPlayerCharacters(Serial accountId)
    {
        var account = _accountStore.Query().FirstOrDefault(a => a.Id == accountId);

        if (account == null)
        {
            return [];
        }

        return [.. account.MobileIds.Select(mobileId => _mobileStore.GetByIdAsync(mobileId).Result)];
    }
}
