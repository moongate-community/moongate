using System.Text;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Handlers.Books;
using Moongate.Server.Ultima.Packets.Books;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Server.Ultima.Handlers.Books;

/// <summary>
///     From the bytes a client sends to the text saved on the book, with the real book service.
/// </summary>
public sealed class BookEditPacketHandlerTests
{
    [Fact]
    public async Task ThePagesAPlayerWrote_AreSavedOnItsBook()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var (handler, book) = await StartAsync(f);
        // Page 2, two lines.
        var packet = Pages(book, "0001" + "0002" + "0002" + Hex("Dear diary,") + "00" + Hex("today") + "00");

        await f.OnLoopAsync(() => handler.Handle(f.Session, packet));

        Assert.Equal("\n\nDear diary,\ntoday", book.GetProp("book.content", ""));
    }

    [Fact]
    public async Task TheTitleAndAuthorAPlayerWrote_AreSaved_FromTheNewPacketAndFromTheOld()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var (handler, book) = await StartAsync(f);
        var body = "01010014" + "0009" + Hex("My diary") + "00" + "0005" + Hex("Aria") + "00";
        Assert.True(BookHeaderChangePacket.TryParse(Convert.FromHexString("D4" + (7 + body.Length / 2).ToString("X4") + $"{book.Id.Value:X8}" + body), out var header));

        await f.OnLoopAsync(() => handler.Handle(f.Session, header));

        Assert.Equal(("My diary", "Aria"), (book.GetProp("book.title", ""), book.GetProp("book.author", "")));

        var old = new byte[99];
        old[0] = 0x93;
        Convert.FromHexString($"{book.Id.Value:X8}").CopyTo(old, 1);
        Encoding.Latin1.GetBytes("Second").CopyTo(old, 9);
        Encoding.Latin1.GetBytes("Bran").CopyTo(old, 69);
        Assert.True(OldBookHeaderChangePacket.TryParse(old, out var oldHeader));

        await f.OnLoopAsync(() => handler.Handle(f.Session, oldHeader));

        Assert.Equal(("Second", "Bran"), (book.GetProp("book.title", ""), book.GetProp("book.author", "")));
    }

    // The older packet holds sixty Latin-1 characters; the book holds sixty bytes of UTF-8, where an accented
    // letter is two. What fits is kept, cut before the letter that does not.
    [Fact]
    public async Task AnOldHeaderOfAccentedLetters_IsCutToWhatTheBookHolds_NotRefused()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var (handler, book) = await StartAsync(f);
        var old = new byte[99];
        old[0] = 0x93;
        Convert.FromHexString($"{book.Id.Value:X8}").CopyTo(old, 1);
        Encoding.Latin1.GetBytes(new string('é', 40)).CopyTo(old, 9);
        Encoding.Latin1.GetBytes(new string('ò', 20)).CopyTo(old, 69);
        Assert.True(OldBookHeaderChangePacket.TryParse(old, out var packet));

        await f.OnLoopAsync(() => handler.Handle(f.Session, packet));

        Assert.Equal((new string('é', 30), new string('ò', 15)), (book.GetProp("book.title", ""), book.GetProp("book.author", "")));
    }

    // A request for a page, a packet that is not well formed, a book that is not there: nothing changes and
    // nothing is thrown.
    [Theory]
    [InlineData("0001" + "0001" + "FFFF")]
    [InlineData("0001" + "0001" + "0009")]
    [InlineData("FFFF")]
    public async Task WhatIsNoEdit_ChangesNothing(string body)
    {
        await using var f = await BookTestFixture.CreateAsync();
        var (handler, book) = await StartAsync(f);
        var missing = new ItemEntity { Id = new(0x4000FFFF), TemplateId = "readable_book" };

        await f.OnLoopAsync(() =>
        {
            handler.Handle(f.Session, Pages(book, body));
            handler.Handle(f.Session, Pages(missing, "0001" + "0001" + "0001" + Hex("x") + "00"));
        });

        Assert.Equal("", book.GetProp("book.content", "x"));
    }

    private static async Task<(BookEditPacketHandler Handler, ItemEntity Book)> StartAsync(BookTestFixture f)
    {
        f.Data.With(new BookTemplate { Id = "blank", Title = "a book", Author = "$player_name", Content = "", ItemTemplate = "readable_book", Writable = true });
        ItemEntity? book = null;
        await f.OnLoopAsync(() => book = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "blank")));

        return (new(f.Books, f.Items, f.World.Mobiles), book!);
    }

    private static BookPagesRequestPacket Pages(ItemEntity book, string body)
    {
        var data = Convert.FromHexString("66" + (7 + body.Length / 2).ToString("X4") + $"{book.Id.Value:X8}" + body);
        Assert.True(BookPagesRequestPacket.TryParse(data, out var packet));

        return packet;
    }

    private static string Hex(string text)
    {
        return Convert.ToHexString(Encoding.UTF8.GetBytes(text));
    }
}
