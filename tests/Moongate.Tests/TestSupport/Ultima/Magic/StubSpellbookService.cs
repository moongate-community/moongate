using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Spells;

namespace Moongate.Tests.TestSupport.Ultima.Magic;

/// <summary>
///     A spellbook service whose books are the items a test names and whose spells are kept in memory: the graphic
///     decides what is a book, a scroll is an item whose graphic is in <see cref="Scrolls" />.
/// </summary>
public sealed class StubSpellbookService : ISpellbookService
{
    /// <summary>
    ///     The spells of each book, by the book's serial value.
    /// </summary>
    public Dictionary<uint, HashSet<int>> Books { get; } = [];

    /// <summary>
    ///     The spell of each scroll graphic.
    /// </summary>
    public Dictionary<int, int> Scrolls { get; } = [];

    /// <summary>
    ///     What <see cref="AddScroll" /> answers when the item is a known scroll.
    /// </summary>
    public SpellbookDropType Drop { get; set; } = SpellbookDropType.Added;

    /// <summary>
    ///     Runs with the scroll when <see cref="AddScroll" /> takes a known scroll, to use it up as the service does.
    /// </summary>
    public Action<ItemEntity>? OnScroll { get; set; }

    public List<(ItemEntity Book, ItemEntity Scroll)> Dropped { get; } = [];

    public List<ItemEntity> Opened { get; } = [];

    public bool IsSpellbook(ItemEntity item)
    {
        return item.ItemId == ISpellbookService.BookGraphic;
    }

    public ulong GetSpells(ItemEntity book)
    {
        return Books.TryGetValue(book.Id.Value, out var spells)
            ? spells.Aggregate(0UL, (mask, spell) => mask | 1UL << (spell - 1))
            : 0;
    }

    public bool Has(ItemEntity book, int spellId)
    {
        return Books.TryGetValue(book.Id.Value, out var spells) && spells.Contains(spellId);
    }

    public bool Add(ItemEntity book, int spellId)
    {
        if (!Books.TryGetValue(book.Id.Value, out var spells))
        {
            Books[book.Id.Value] = spells = [];
        }

        return spells.Add(spellId);
    }

    /// <summary>
    ///     What <see cref="IsCarriedBy" /> answers, when a test sets it; else a book is carried when it is in
    ///     <see cref="Carried" />.
    /// </summary>
    public Func<MobileEntity, ItemEntity, bool>? IsCarried { get; set; }

    public bool IsCarriedBy(MobileEntity mobile, ItemEntity book)
    {
        return IsCarried?.Invoke(mobile, book) ?? Carried.Contains(book);
    }

    public ItemEntity? FindCarried(MobileEntity mobile, int spellId)
    {
        return Carried.FirstOrDefault(book => spellId == 0 || Has(book, spellId));
    }

    /// <summary>
    ///     The books the mobile carries, as <see cref="FindCarried" /> sees them.
    /// </summary>
    public List<ItemEntity> Carried { get; } = [];

    public void Open(GameSession session, ItemEntity book)
    {
        Opened.Add(book);
    }

    public bool TryOpen(GameSession session, MobileEntity player, ItemEntity book)
    {
        Opened.Add(book);

        return true;
    }

    public SpellbookDropType AddScroll(MobileEntity player, ItemEntity book, ItemEntity scroll)
    {
        if (!IsSpellbook(book) || !Scrolls.ContainsKey(scroll.ItemId))
        {
            return SpellbookDropType.Ignored;
        }

        Dropped.Add((book, scroll));
        OnScroll?.Invoke(scroll);

        return Drop;
    }
}
