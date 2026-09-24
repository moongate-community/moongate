using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.Auth;

namespace Moongate.Server.Ultima.Data.Account;

/// <summary>A bounded account page with an exclusive continuation cursor.</summary>
public sealed class AccountPage
{
    public IReadOnlyList<AccountEntity> Items { get; }
    public Serial NextAfterId { get; }

    public AccountPage(IReadOnlyList<AccountEntity> items, Serial nextAfterId)
    {
        Items = items;
        NextAfterId = nextAfterId;
    }
}
