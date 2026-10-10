using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Spells;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     What a spellbook holds and what a player does with it: the 64 spells it keeps as a mask in a prop of the item over
///     the <c>spells</c> tag of its template, opening it on the client, and adding the spell of a scroll dropped on it.
/// </summary>
/// <remarks>
///     Game loop only.
/// </remarks>
public interface ISpellbookService
{
    /// <summary>
    ///     The graphic of a spellbook of Magery: an item with it is a spellbook.
    /// </summary>
    const int BookGraphic = 0x0EFA;

    /// <summary>
    ///     The prop that keeps the spells a book holds, a mask where bit 0 is the spell 1. A book that has none holds what
    ///     the <c>spells</c> tag of its template says, or nothing.
    /// </summary>
    const string SpellsProp = "spellbook.spells";

    /// <summary>
    ///     The tag of an item template that gives a new book its spells, as the decimal number of the mask.
    /// </summary>
    const string SpellsTag = "spells";

    /// <summary>
    ///     The gump a client opens as a spellbook (0x24).
    /// </summary>
    const int BookGump = 0xFFFF;

    /// <summary>
    ///     The sound of a scroll that joins a book.
    /// </summary>
    const int ScrollAddedSound = 0x249;

    /// <summary>
    ///     "That spell is already present in that spellbook."
    /// </summary>
    const int AlreadyPresentMessage = 500179;

    /// <summary>
    ///     "The spellbook must be in your backpack (and not in a container within) to open."
    /// </summary>
    const int MustBeCarriedMessage = 500207;

    /// <summary>
    ///     Gets whether the item is a spellbook.
    /// </summary>
    bool IsSpellbook(ItemEntity item);

    /// <summary>
    ///     Gets the mask of the spells the book holds, where bit 0 is the spell 1.
    /// </summary>
    ulong GetSpells(ItemEntity book);

    /// <summary>
    ///     Gets whether the book holds the spell, by its client number 1 to 64.
    /// </summary>
    bool Has(ItemEntity book, int spellId);

    /// <summary>
    ///     Puts the spell in the book and shows the book again. False, with nothing changed, for a number outside 1 to 64,
    ///     a spell the book holds already, or an item that is no spellbook.
    /// </summary>
    bool Add(ItemEntity book, int spellId);

    /// <summary>
    ///     Gets whether the mobile wears the book or carries it in its backpack, not in a bag inside it.
    /// </summary>
    bool IsCarriedBy(MobileEntity mobile, ItemEntity book);

    /// <summary>
    ///     Finds the spellbook of a mobile that holds the spell: the one it wears, else one in its backpack. Null when it
    ///     carries none; with <paramref name="spellId" /> 0, the first book of any kind.
    /// </summary>
    ItemEntity? FindCarried(MobileEntity mobile, int spellId);

    /// <summary>
    ///     Opens the book on the client of the session: the gump (0x24) and the spells it holds (0x3C), as the classic client
    ///     reads them. It checks nothing: whoever calls it has decided the player may see it.
    /// </summary>
    void Open(GameSession session, ItemEntity book);

    /// <summary>
    ///     What a player's double click on a book does: it opens when the player wears it or carries it in its backpack, else
    ///     the player is told it must be carried.
    /// </summary>
    /// <returns>
    ///     Whether the book was opened.
    /// </returns>
    bool TryOpen(GameSession session, MobileEntity player, ItemEntity book);

    /// <summary>
    ///     Adds the spell of a scroll the player dropped on the book it carries: one scroll of the stack is used up, the book
    ///     shows the sound of it, and a spell the book holds already is refused with a message.
    /// </summary>
    SpellbookDropType AddScroll(MobileEntity player, ItemEntity book, ItemEntity scroll);
}
