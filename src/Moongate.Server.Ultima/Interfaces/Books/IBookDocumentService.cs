using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces.Books;

/// <summary>
///     Creates, inscribes and displays readable item snapshots on the game loop.
/// </summary>
public interface IBookDocumentService
{
    /// <summary>
    ///     Gives a resolved document to the recipient; failure creates no item.
    /// </summary>
    ItemEntity? Give(
        MobileEntity recipient, string templateId, IReadOnlyDictionary<string, object?>? values = null,
        string? recordedPlayerName = null
    );

    /// <summary>
    ///     Writes all resolved fields to a supported item; failure leaves it unchanged.
    /// </summary>
    bool Write(
        ItemEntity item, MobileEntity recipient, string templateId, IReadOnlyDictionary<string, object?>? values = null,
        string? recordedPlayerName = null
    );

    /// <summary>
    ///     Opens saved text for an eligible reader. From Lua, true means queued; access is checked again before delivery.
    /// </summary>
    bool Open(ItemEntity item, MobileEntity reader);

    /// <summary>
    ///     Sets the title and the author a player wrote on a writable book it carries. False, with nothing changed,
    ///     for a book that is not writable or not carried by <paramref name="writer" />, and for a text the client's
    ///     fields do not hold.
    /// </summary>
    bool SetHeader(ItemEntity book, MobileEntity writer, string title, string author);

    /// <summary>
    ///     Sets the pages a player wrote in a writable book it carries; a page with no lines given is a request of
    ///     the client and is left as it is. All or nothing: false, with nothing changed, as
    ///     <see cref="SetHeader" />, and for a page or a line the book does not hold.
    /// </summary>
    bool SetPages(ItemEntity book, MobileEntity writer, IReadOnlyList<BookPageEdit> pages);
}
