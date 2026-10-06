using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Packets;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Packets.Books;

namespace Moongate.Server.Ultima.Handlers.Books;

/// <summary>
///     What a player wrote in a book: its title and author (0xD4, or 0x93 from the older clients) and its pages
///     (0x66). The book service decides whether that player may write in that book; a packet for anything else,
///     or one that is not well formed, changes nothing and is answered with nothing.
/// </summary>
public sealed class BookEditPacketHandler
    : IPacketHandler<BookPagesRequestPacket>, IPacketHandler<BookHeaderChangePacket>, IPacketHandler<OldBookHeaderChangePacket>
{
    private readonly IBookDocumentService _books;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;

    public BookEditPacketHandler(IBookDocumentService books, IItemService items, IMobileService mobiles)
    {
        _books = books;
        _items = items;
        _mobiles = mobiles;
    }

    public void Handle(GameSession session, BookPagesRequestPacket packet)
    {
        if (packet.Pages.Count > 0 &&
            _items.TryGet(packet.Book, out var book) &&
            _mobiles.TryGet(session.CharacterId, out var writer))
        {
            _books.SetPages(book, writer, packet.Pages);
        }
    }

    public void Handle(GameSession session, BookHeaderChangePacket packet)
    {
        if (packet.Title is { } title && packet.Author is { } author)
        {
            SetHeader(session, packet.Book, title, author);
        }
    }

    public void Handle(GameSession session, OldBookHeaderChangePacket packet)
    {
        // Sixty and thirty Latin-1 characters may be more bytes of UTF-8 than a book holds: what fits is kept.
        SetHeader(
            session,
            packet.Book,
            BookHeaderPacket.Fit(packet.Title, BookHeaderPacket.TitleBytes),
            BookHeaderPacket.Fit(packet.Author, BookHeaderPacket.AuthorBytes)
        );
    }

    private void SetHeader(GameSession session, Serial serial, string title, string author)
    {
        if (_items.TryGet(serial, out var book) && _mobiles.TryGet(session.CharacterId, out var writer))
        {
            _books.SetHeader(book, writer, title, author);
        }
    }
}
