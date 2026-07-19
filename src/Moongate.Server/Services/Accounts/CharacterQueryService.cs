using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using SquidStd.Persistence.Abstractions.Data;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.Accounts;

/// <summary>
/// Pages player characters out of the mobile store. Nothing on a mobile says it is a player character —
/// the only link is the owning account's id list — so the accounts are read first, both to know which
/// mobiles to keep and to name each character's owner.
/// </summary>
public sealed class CharacterQueryService : ICharacterQueryService
{
    private readonly IAccountService _accounts;
    private readonly IEntityStore<MobileEntity, Serial> _mobileStore;

    public CharacterQueryService(IAccountService accounts, IPersistenceService persistenceService)
    {
        _accounts = accounts;
        _mobileStore = persistenceService.GetStore<MobileEntity, Serial>();
    }

    public PagedResult<OwnedCharacter> Search(string? search, int skip, int take)
    {
        // One pass over the accounts, needed either way: it is what tells a character from an NPC, and the
        // admin list has to name each character's owner regardless. Accounts are few and small next to the
        // mobile store, which holds every NPC on the shard.
        var owners = new Dictionary<Serial, string>();

        foreach (var account in _accounts.GetAll())
        {
            foreach (var mobileId in account.MobileIds)
            {
                owners[mobileId] = account.Username;
            }
        }

        // QueryPaged filters the live entities under the store's lock and clones only the page. Filtering
        // over GetAll() or Query() instead would deep-clone every mobile on the shard first, which is the
        // cost this exists to avoid.
        var page = _mobileStore.QueryPaged(
            mobile => owners.TryGetValue(mobile.Id, out var username) && Matches(mobile, username, search),
            mobile => mobile.Name,
            skip,
            take
        );

        IReadOnlyList<OwnedCharacter> items =
            [.. page.Items.Select(mobile => new OwnedCharacter(mobile, owners[mobile.Id]))];

        return new(items, page.Total, page.Skip, page.Take);
    }

    /// <summary>
    /// One box, matched against the character's name and its owner's username: the staff question is "find
    /// me so-and-so's character", and which of the two names they typed is not worth asking.
    /// </summary>
    private static bool Matches(MobileEntity mobile, string username, string? search)
        => string.IsNullOrEmpty(search) ||
           mobile.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
           username.Contains(search, StringComparison.OrdinalIgnoreCase);
}
