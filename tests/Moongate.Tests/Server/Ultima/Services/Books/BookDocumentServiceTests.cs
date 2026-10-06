using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Packets.Books;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services.Books;

public sealed class BookDocumentServiceTests
{

    // The cover of a book is told by its source: the item it is written on takes that graphic.
    [Fact]
    public async Task Give_ASourceWithAnItemId_GivesAnItemOfThatGraphic()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(
            new BookTemplate
                { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book", ItemId = 0x0FF2 },
            new BookTemplate { Id = "plain", Title = "Plain", Content = "Text", ItemTemplate = "readable_book" }
        );

        await f.OnLoopAsync(() =>
            {
                var tome = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "tome"));
                f.Serials.Serials.Enqueue(new(0x40000F01));
                var plain = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "plain"));

                Assert.Equal(("readable_book", 0x0FF2), (tome.TemplateId, tome.ItemId));
                Assert.Equal(0x0FF1, plain.ItemId);
            }
        );
    }

    // A book item opens the client's book: its cover, then every page; no parchment.
    [Fact]
    public async Task Open_ABook_SendsItsHeaderAndItsPages_AndOpensNoGump()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(
            new BookTemplate
            {
                Id = "tome", Title = "Tome", Author = "Yorick", Content = "one\ntwo\n\nthree", ItemTemplate = "readable_book"
            }
        );

        await f.OnLoopAsync(() =>
            {
                var tome = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "tome"));
                f.World.Sender.Sent.Clear();

                Assert.True(f.Books.Open(tome, f.Player));

                Assert.Empty(f.Gumps.Opened);
                var header = Assert.IsType<BookHeaderPacket>(f.World.Sender.Sent[0]);
                var pages = Assert.IsType<BookPagesPacket>(f.World.Sender.Sent[1]);
                Assert.Equal(2, f.World.Sender.Sent.Count);
                Assert.Equal((tome.Id, 2), (header.Book, header.PageCount));
                Assert.Equal((tome.Id, 2), (pages.Book, pages.PageCount));
            }
        );
    }

    [Fact]
    public async Task Open_AScroll_StillOpensTheParchment_AndSendsNoBook()
    {
        await using var f = await BookTestFixture.CreateAsync();

        await f.OnLoopAsync(() =>
            {
                var letter = f.Give();
                f.World.Sender.Sent.Clear();

                Assert.True(f.Books.Open(letter, f.Player));

                Assert.Single(f.Gumps.Opened);
                Assert.DoesNotContain(f.World.Sender.Sent, packet => packet is BookHeaderPacket or BookPagesPacket);
            }
        );
    }

    // Written on a scroll from a source that has a cover: the scroll takes the graphic and stays a parchment.
    [Fact]
    public async Task Open_AScrollWrittenFromABookSource_IsStillAParchment()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(
            f.Source,
            new BookTemplate
                { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book", ItemId = 0x0FF2 }
        );

        await f.OnLoopAsync(() =>
            {
                var letter = f.Give();
                Assert.True(f.Books.Write(letter, f.Player, "tome"));
                f.World.Sender.Sent.Clear();

                Assert.True(f.Books.Open(letter, f.Player));

                Assert.Single(f.Gumps.Opened);
                Assert.DoesNotContain(f.World.Sender.Sent, packet => packet is BookHeaderPacket);
            }
        );
    }

    [Theory]
    [InlineData("other")]
    [InlineData("far")]
    public async Task Open_ABookOutOfReach_SendsNothing(string where)
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(new BookTemplate { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book" });

        await f.OnLoopAsync(() =>
            {
                var tome = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "tome"));

                if (where == "far")
                {
                    f.Items.PlaceOnGround(tome, MapType.Trammel, new(1700, 1700, 0));
                }

                f.World.Sender.Sent.Clear();

                Assert.False(f.Books.Open(tome, where == "other" ? f.Other : f.Player));

                Assert.DoesNotContain(f.World.Sender.Sent, packet => packet is BookHeaderPacket or BookPagesPacket);
            }
        );
    }

    [Fact]
    public async Task Open_ABookOfMorePagesThanTheClientTakes_IsRefused()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var content = string.Join("\n\n", Enumerable.Range(1, BookPagination.MaxPages + 1).Select(page => $"p{page}"));
        f.Data.With(new BookTemplate { Id = "tome", Title = "Tome", Content = content, ItemTemplate = "readable_book" });

        await f.OnLoopAsync(() =>
            {
                var tome = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "tome"));
                f.World.Sender.Sent.Clear();

                Assert.False(f.Books.Open(tome, f.Player));

                Assert.DoesNotContain(f.World.Sender.Sent, packet => packet is BookHeaderPacket or BookPagesPacket);
            }
        );
    }

    // What makes a book one a player writes in is on the item, and goes when a read-only text is written on it.
    [Fact]
    public async Task Give_AWritableSource_MarksTheItem_AndAReadOnlyTextWrittenOnItUnmarksIt()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(
            new BookTemplate
            {
                Id = "blank", Title = "a book", Author = "$player_name", Content = "", ItemTemplate = "readable_book",
                Writable = true, Pages = 30
            },
            new BookTemplate { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book" }
        );

        await f.OnLoopAsync(() =>
            {
                var blank = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "blank"));

                Assert.Equal(
                    (true, 30L, "Pippo", ""),
                    (blank.GetProp("book.writable", false), blank.GetProp("book.pages", 0L),
                        blank.GetProp("book.author", ""),
                        blank.GetProp("book.content", "x"))
                );

                Assert.True(f.Books.Write(blank, f.Player, "tome"));

                Assert.False(blank.TryGetProp<bool>("book.writable", out _));
                Assert.False(blank.TryGetProp<long>("book.pages", out _));
            }
        );
    }

    // The character that carries a writable book opens it for writing, with all its pages, blank ones too.
    [Fact]
    public async Task Open_AWritableBookByItsCarrier_IsWritable_WithAllItsPages()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                f.World.Sender.Sent.Clear();

                Assert.True(f.Books.Open(blank, f.Player));

                var header = Assert.Single(f.World.Sender.Sent.OfType<BookHeaderPacket>());
                Assert.Equal((true, 30), (header.Writable, header.PageCount));
                Assert.Equal(30, Assert.Single(f.World.Sender.Sent.OfType<BookPagesPacket>()).PageCount);
            }
        );
    }

    // Lying on the ground, anyone near reads it and nobody writes in it.
    [Fact]
    public async Task Open_AWritableBookNobodyCarries_IsReadOnly()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                f.Items.PlaceOnGround(blank, MapType.Trammel, f.Player.Location);
                f.World.Sender.Sent.Clear();

                Assert.True(f.Books.Open(blank, f.Player));

                Assert.False(Assert.Single(f.World.Sender.Sent.OfType<BookHeaderPacket>()).Writable);
                Assert.False(f.Books.SetHeader(blank, f.Player, "Mine", "Me"));
                Assert.False(f.Books.SetPages(blank, f.Player, [new(1, ["no"])]));
                Assert.Equal("", blank.GetProp("book.content", "x"));
            }
        );
    }

    [Fact]
    public async Task SetHeader_ByTheCarrier_SetsTitleAndAuthor_AndTheNameFollows()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                f.World.Sender.Sent.Clear();

                Assert.True(f.Books.SetHeader(blank, f.Player, "My diary", "Aria"));

                Assert.Equal(
                    ("My diary", "Aria", "My diary"),
                    (blank.GetProp("book.title", ""), blank.GetProp("book.author", ""), blank.Name)
                );
                // Its carrier sees the new name.
                Assert.NotEmpty(f.World.Sender.Sent);
            }
        );
    }

    [Theory]
    // More than the client's fields hold, counted in bytes, and what is not text.
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "A")]
    [InlineData("èèèèèèèèèèèèèèèèèèèèèèèèèèèèèèè", "A")]
    [InlineData("T", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("two\nlines", "A")]
    [InlineData("T", "tab\there")]
    public async Task SetHeader_WhatTheClientCannotHold_ChangesNothing(string title, string author)
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                Assert.False(f.Books.SetHeader(blank, f.Player, title, author));

                Assert.Equal(("a book", "Pippo"), (blank.GetProp("book.title", ""), blank.GetProp("book.author", "")));
            }
        );
    }

    // A title wiped out: the book is called as its kind is.
    [Fact]
    public async Task SetHeader_ABlankTitle_LeavesTheNameOfTheItemTemplate()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                Assert.True(f.Books.SetHeader(blank, f.Player, "", ""));

                Assert.Equal(("", null), (blank.GetProp("book.title", "x"), blank.Name));
            }
        );
    }

    // What is written is what is read back: a blank line inside a page stays in its page.
    [Fact]
    public async Task SetPages_ByTheCarrier_WritesThePages_AndOpeningGivesThemBack()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                Assert.True(
                    f.Books.SetPages(blank, f.Player, [new(1, ["Dear diary,", "", "today"]), new(3, ["the end", ""])])
                );

                Assert.Equal("Dear diary,\n \ntoday\n\n\n\nthe end", blank.GetProp("book.content", ""));

                // A second edit touches its page alone.
                Assert.True(f.Books.SetPages(blank, f.Player, [new(2, ["middle"])]));

                Assert.Equal("Dear diary,\n \ntoday\n\nmiddle\n\nthe end", blank.GetProp("book.content", ""));
                f.World.Sender.Sent.Clear();
                Assert.True(f.Books.Open(blank, f.Player));
                Assert.Equal(30, Assert.Single(f.World.Sender.Sent.OfType<BookPagesPacket>()).PageCount);
            }
        );
    }

    // The longest line the client sends, 79 characters, is saved and read back as one line: a page of eight of
    // them stays one page, and the page after it stays where it was.
    [Fact]
    public async Task SetPages_LinesOfTheLongestLength_ComeBackAsTheyWereWritten()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);
        var full = Enumerable.Repeat(new string('x', 79), 8).ToArray();

        await f.OnLoopAsync(() =>
            {
                Assert.True(f.Books.SetPages(blank, f.Player, [new(1, full), new(2, ["second page"])]));

                Assert.True(BookPagination.TryPaginate(blank.GetProp("book.content", ""), out var pages));
                Assert.Equal(full, pages[0]);
                Assert.Equal(["second page"], pages[1]);
                Assert.Equal(2, pages.Count);
            }
        );
    }

    // A bank that is closed is out of reach, for the pen as for the eye.
    [Fact]
    public async Task SetPages_ABookInTheWritersClosedBank_ChangesNothing()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                f.Player.AccountId = new(1);
                var bank = new ItemEntity { Id = new(0x40003000), TemplateId = "backpack", ItemId = 0xE75, Amount = 1 };
                bank.Equip(f.Player.Id, LayerType.Bank);
                f.Items.Add([bank]);
                f.Items.MoveToContainer(blank, bank.Id, new(10, 10));

                Assert.False(f.Books.SetPages(blank, f.Player, [new(1, ["hidden"])]));
                Assert.False(f.Books.SetHeader(blank, f.Player, "Hidden", "Me"));

                Assert.Equal(("", "a book"), (blank.GetProp("book.content", "x"), blank.GetProp("book.title", "")));
            }
        );
    }

    // Props a script or the staff set wrongly: the book is simply not one to write in, and nothing is thrown out
    // of the handler of a packet.
    [Theory]
    [InlineData("book.writable", "yes")]
    [InlineData("book.pages", "many")]
    public async Task ABookWhoseWritingPropsAreNotWhatTheyShouldBe_IsNotWritten_AndStillOpens(string prop, string value)
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                blank.SetProp(prop, value);

                var written = f.Books.SetPages(blank, f.Player, [new(1, ["x"])]);
                f.Books.SetHeader(blank, f.Other, "T", "A");

                Assert.Equal(prop == "book.pages", written);
                Assert.True(f.Books.Open(blank, f.Player));
            }
        );
    }

    // As ModernUO: what a player writes on the cover cannot be markup, nor the number of a text of the client.
    [Fact]
    public async Task SetHeader_MarkupAndClilocSigns_AreReplaced()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                Assert.True(f.Books.SetHeader(blank, f.Player, "#1042971", "<b>Aria</b>"));

                Assert.Equal(
                    ("-1042971", "(b)Aria(/b)", "-1042971"),
                    (blank.GetProp("book.title", ""), blank.GetProp("book.author", ""), blank.Name)
                );
            }
        );
    }

    // A request for a page is no edit.
    [Fact]
    public async Task SetPages_OnlyRequests_ChangeNothing()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                Assert.True(f.Books.SetPages(blank, f.Player, [new(1, ["kept"])]));

                Assert.False(f.Books.SetPages(blank, f.Player, [new(1, null), new(2, null)]));

                Assert.Equal("kept", blank.GetProp("book.content", ""));
            }
        );
    }

    [Theory]
    [InlineData("page 0")]
    [InlineData("page beyond")]
    [InlineData("nine lines")]
    [InlineData("long line")]
    [InlineData("control")]
    [InlineData("one bad page among good ones")]
    public async Task SetPages_WhatIsNotAPageOfTheBook_ChangesNothing(string what)
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);
        IReadOnlyList<BookPageEdit> edit = what switch
        {
            "page 0"      => [new(0, ["x"])],
            "page beyond" => [new(31, ["x"])],
            "nine lines"  => [new(1, Enumerable.Repeat("x", 9).ToArray())],
            "long line"   => [new(1, [new string('x', 80)])],
            "control"     => [new(1, ["bell\a"])],
            _             => [new(1, ["good"]), new(99, ["bad"])]
        };

        await f.OnLoopAsync(() =>
            {
                Assert.False(f.Books.SetPages(blank, f.Player, edit));

                Assert.Equal("", blank.GetProp("book.content", "x"));
            }
        );
    }

    // Not the carrier, a book that is not writable, a scroll, a book on a cursor.
    [Theory]
    [InlineData("other")]
    [InlineData("read only")]
    [InlineData("scroll")]
    [InlineData("held")]
    public async Task SetHeaderAndSetPages_ByWhoMayNotWrite_ChangeNothing(string who)
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                var book = blank;

                if (who == "read only")
                {
                    Assert.True(f.Books.Write(blank, f.Player, "tome"));
                }
                else if (who == "scroll")
                {
                    f.Serials.Serials.Enqueue(new(0x40000F02));
                    book = f.Give();
                    book.SetProp("book.writable", true);
                }
                else if (who == "held")
                {
                    f.Session.Set(ItemSessionKeys.Held, new HeldItem(blank.Id));
                }

                var writer = who == "other" ? f.Other : f.Player;
                var before = (book.GetProp("book.title", ""), book.GetProp("book.content", ""));

                Assert.False(f.Books.SetHeader(book, writer, "Mine", "Me"));
                Assert.False(f.Books.SetPages(book, writer, [new(1, ["mine"])]));

                Assert.Equal(before, (book.GetProp("book.title", ""), book.GetProp("book.content", "")));
            }
        );
    }

    // Traded, the book is written by who carries it now.
    [Fact]
    public async Task SetPages_AfterTheBookChangesHands_OnlyTheNewCarrierWrites()
    {
        await using var f = await BookTestFixture.CreateAsync();
        var blank = await BlankBookAsync(f);

        await f.OnLoopAsync(() =>
            {
                var theirs = new ItemEntity { Id = new(0x40002100), TemplateId = "backpack", ItemId = 0xE75, Amount = 1 };
                theirs.Equip(f.Other.Id, LayerType.Backpack);
                f.Items.Add([theirs]);
                f.Items.MoveToContainer(blank, theirs.Id, new(10, 10));

                Assert.False(f.Books.SetPages(blank, f.Player, [new(1, ["old owner"])]));
                Assert.True(f.Books.SetPages(blank, f.Other, [new(1, ["new owner"])]));

                Assert.Equal("new owner", blank.GetProp("book.content", ""));
            }
        );
    }

    // A text beyond what a document holds is refused whole.
    [Fact]
    public async Task SetPages_BeyondTheLengthOfADocument_ChangesNothing()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(
            new BookTemplate
                { Id = "thick", Title = "Thick", Content = "", ItemTemplate = "readable_book", Writable = true, Pages = 255 }
        );

        await f.OnLoopAsync(() =>
            {
                var thick = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "thick"));
                var line = new string('x', 79);
                var full = Enumerable.Range(1, 26)
                    .Select(page => new BookPageEdit(page, Enumerable.Repeat(line, 8).ToArray()))
                    .ToArray();

                Assert.True(f.Books.SetPages(thick, f.Player, full[..25]));
                var before = thick.GetProp("book.content", "");

                Assert.False(f.Books.SetPages(thick, f.Player, full));

                Assert.Equal(before, thick.GetProp("book.content", ""));
            }
        );
    }

    [Fact]
    public async Task Write_ASourceWithAnItemId_SetsTheGraphicOfTheItemWrittenOn()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(
            f.Source,
            new BookTemplate
                { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book", ItemId = 0x0FF2 }
        );

        await f.OnLoopAsync(() =>
            {
                var letter = f.Give();

                Assert.True(f.Books.Write(letter, f.Player, "tome"));
                Assert.Equal(0x0FF2, letter.ItemId);
            }
        );
    }

    [Theory]
    [InlineData("own", true)]
    [InlineData("nested", true)]
    [InlineData("plain", false)]
    [InlineData("malformed", false)]
    [InlineData("ground", false)]
    [InlineData("claimed", false)]
    public async Task Open_ClaimActionOnlyForEligibleBackpackLetter(string state, bool expected)
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        if (state == "claimed") await await f.BeginAsync();
        await f.Books.OnLoopAsync(() =>
            {
                if (state == "plain") f.Letter.RemoveProp(BookAttachmentCodec.PropKey);
                if (state == "malformed") f.Letter.SetProp(BookAttachmentCodec.PropKey, "{}");
                if (state == "ground") f.Books.Items.PlaceOnGround(f.Letter, MapType.Trammel, f.Books.Player.Location);
                if (state == "nested")
                {
                    var bag = new ItemEntity { Id = new(0x40002000), TemplateId = "backpack", ItemId = 0xE75 };
                    bag.PutInContainer(f.Books.Backpack.Id, new(10, 10));
                    f.Books.Items.Add([bag]);
                    f.Books.Items.MoveToContainer(f.Letter, bag.Id, new(10, 10));
                }

                Assert.True(f.Books.Books.Open(f.Letter, f.Books.Player));
                var built = Assert.Single(f.Books.Gumps.Opened).Gump.Layout.Build();
                Assert.Equal(expected, built.Buttons.Contains(1));
                if (expected) Assert.Contains("Ritira allegati", built.Strings);
            }
        );
    }

    [Fact]
    public async Task Claim_ReservedInventoryRefusesBankTransfersBeforeAnySideEffect()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.Block = true;
        var bankGold = new ItemEntity { Id = new(0x40003001), TemplateId = "gold", ItemId = 0xEED, Amount = 200 };
        var carriedGold = new ItemEntity { Id = new(0x40003002), TemplateId = "gold", ItemId = 0xEED, Amount = 10 };
        await f.Books.OnLoopAsync(() =>
            {
                f.Books.Player.AccountId = new(1);
                var bank = new ItemEntity { Id = new(0x40003000), TemplateId = "backpack", ItemId = 0xE75 };
                bank.Equip(f.Books.Player.Id, LayerType.Bank);
                bankGold.PutInContainer(bank.Id, new(10, 10));
                carriedGold.PutInContainer(f.Books.Backpack.Id, new(10, 10));
                f.Books.Items.Add([bank, bankGold, carriedGold]);
            }
        );
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.OnLoopAsync(() =>
            {
                Assert.Equal(
                    Moongate.Server.Ultima.Types.Bank.BankResultType.Busy,
                    f.Books.Bank.Withdraw(f.Books.Player, 1)
                );
                Assert.Equal(Moongate.Server.Ultima.Types.Bank.BankResultType.Busy, f.Books.Bank.Deposit(f.Books.Player, 1));
                Assert.Equal((200, 10), (bankGold.Amount, carriedGold.Amount));
            }
        );
        f.Store.Continue.TrySetResult();
        await pending;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_ReservedInventoryRefusesBankChecksBeforeAnySideEffect(bool cash)
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.Block = true;
        var bank = new ItemEntity { Id = new(0x40003000), TemplateId = "backpack", ItemId = 0xE75 };
        var gold = new ItemEntity { Id = new(0x40003001), TemplateId = "gold", ItemId = 0xEED, Amount = 20000 };
        var check = new ItemEntity { Id = new(0x40003002), TemplateId = BankService.CheckTemplate, ItemId = 0x14F0 };
        await f.Books.OnLoopAsync(() =>
            {
                f.Books.Player.AccountId = new(1);
                bank.Equip(f.Books.Player.Id, LayerType.Bank);
                gold.PutInContainer(bank.Id, new(10, 10), 0);
                check.PutInContainer(bank.Id, new(20, 20), 1);
                check.SetProp(ItemPropKeys.BankWorth, 5000L);
                f.Books.Items.Add([bank, gold, check]);
            }
        );
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.OnLoopAsync(() =>
            {
                var serials = f.Books.Serials.Serials.Count;
                if (cash)
                {
                    Assert.Equal(
                        Moongate.Server.Ultima.Types.Bank.BankResultType.Busy,
                        f.Books.Bank.Cash(f.Books.Player, check, out var deposited)
                    );
                    Assert.Equal(0, deposited);
                }
                else
                {
                    Assert.Equal(
                        Moongate.Server.Ultima.Types.Bank.BankResultType.Busy,
                        f.Books.Bank.WriteCheck(f.Books.Player, 5000)
                    );
                }

                Assert.Equal(20000, gold.Amount);
                Assert.Equal(5000L, check.GetProp<long>(ItemPropKeys.BankWorth));
                Assert.True(f.Books.Items.TryGet(check.Id, out _));
                Assert.Equal(serials, f.Books.Serials.Serials.Count);
            }
        );
        f.Store.Continue.TrySetResult();
        await pending;
    }

    [Fact]
    public async Task Give_AttachmentsFreezeWithoutCreatingRewardItemsOrChangingLetterWeight()
    {
        await using var f = await BookTestFixture.CreateAsync();
        await f.OnLoopAsync(() =>
            {
                f.Source.Attachments.Add(
                    new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100"), Hue = HueSpec.FromValue(42) }
                );
                var letter = f.Give();
                Assert.Equal(2, f.Items.Items.Count);
                Assert.Equal(
                    1m,
                    new WeightService(
                        f.Items,
                        f.ItemTemplates,
                        new FakeTileDataService().Item(0x14ED, TileFlagType.None, 1)
                    ).Of(letter)
                );
                var payload = Assert.IsType<string>(letter.Props!["book.attachments"]);
                Assert.True(BookAttachmentCodec.TryDecode(payload, out var batch));
                Assert.Equal(100, Assert.Single(batch!.Items).Amount);
                Assert.Equal(42, Assert.Single(batch.Items).Hue);
                f.Source.Attachments[0].Amount = DiceSpec.Parse("200");
                Assert.Equal(payload, letter.GetProp<string>("book.attachments"));
            }
        );
    }

    [Fact]
    public async Task Write_PlainLetterCannotAcquireNewRewardsAndExistingBatchSurvivesRewriting()
    {
        await using var f = await BookTestFixture.CreateAsync();
        await f.OnLoopAsync(() =>
            {
                var plain = f.Give();
                f.Source.Attachments.Add(new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100") });
                f.Source.Content = "Changed body";
                Assert.False(
                    f.Books.Write(
                        plain,
                        f.Player,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Vega" }
                    )
                );
                Assert.Equal("Dear Pippo,\n\nBring this to Vega.", plain.GetProp<string>("book.content"));
                f.Serials.Serials.Enqueue(new(0x40001000));
                var gift = f.Give();
                var original = gift.GetProp<string>("book.attachments");
                f.Source.Attachments[0].Amount = DiceSpec.Parse("999");
                Assert.True(
                    f.Books.Write(
                        gift,
                        f.Other,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Vega" }
                    )
                );
                Assert.Equal(original, gift.GetProp<string>("book.attachments"));
            }
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Write_MalformedPayloadCannotRegenerate(bool wrongKind)
    {
        await using var f = await BookTestFixture.CreateAsync();
        await f.OnLoopAsync(() =>
            {
                var letter = f.Give();
                letter.Props!["book.attachments"] = wrongKind ? 42 : "{}";
                f.Source.Content = "Replaced";
                Assert.False(
                    f.Books.Write(
                        letter,
                        f.Player,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Vega" }
                    )
                );
                Assert.Equal("Dear Pippo,\n\nBring this to Vega.", letter.GetProp<string>("book.content"));
            }
        );
    }

    [Fact]
    public async Task Write_ClaimedLetterNeverReplenishesRewards()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        var payload = f.Letter.GetProp<string>(BookAttachmentCodec.PropKey);
        Assert.Equal(Moongate.Server.Ultima.Types.Books.BookAttachmentClaimResultType.Claimed, await await f.BeginAsync());
        await f.Books.OnLoopAsync(() =>
            {
                f.Books.Source.Attachments.Add(new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("999") });
                Assert.True(
                    f.Books.Books.Write(
                        f.Letter,
                        f.Books.Player,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Vega" }
                    )
                );
                Assert.Equal(payload, f.Letter.GetProp<string>(BookAttachmentCodec.PropKey));
                Assert.False(f.Service.CanClaim(f.Letter, f.Books.Session));
                Assert.Equal(3, f.Books.Items.Items.Count);
            }
        );
    }

    [Fact]
    public async Task Write_ReservedInventoryLeavesTextIntact()
    {
        await using var f = await BookTestFixture.CreateAsync();
        await f.OnLoopAsync(() =>
            {
                var letter = f.Give();
                f.Reservations.TryReserve(f.Player.Id, Task.CompletedTask);
                f.Source.Content = "Replaced";
                Assert.False(
                    f.Books.Write(
                        letter,
                        f.Player,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Vega" }
                    )
                );
                Assert.Equal("Dear Pippo,\n\nBring this to Vega.", letter.GetProp<string>("book.content"));
            }
        );
    }

    [Fact]
    public async Task Give_InvalidValues_CreatesNothingAndConsumesNoSerial()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                Assert.Null(fixture.Books.Give(fixture.Player, "welcome_letter"));
                Assert.Null(fixture.Books.Give(fixture.Player, "missing"));
                Assert.Single(fixture.Serials.Serials);
                Assert.Single(fixture.Items.Items);
            }
        );
    }

    [Fact]
    public async Task Give_ValidDocument_SavesPlainSnapshotAndRefreshesName()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                Assert.Equal("Welcome Pippo", note.Name);
                Assert.Equal("welcome_letter", note.GetProp<string>("book.template"));
                Assert.Equal("Welcome Pippo", note.GetProp<string>("book.title"));
                Assert.Equal("British", note.GetProp<string>("book.author"));
                Assert.Equal("Dear Pippo,\n\nBring this to Vega.", note.GetProp<string>("book.content"));
                Assert.Equal(fixture.Backpack.Id, note.ContainerId);
                Assert.Empty(fixture.Serials.Serials);
                Assert.False(note.Props!.ContainsKey(BookAttachmentCodec.PropKey));
            }
        );
    }

    [Theory]
    [InlineData("held")]
    [InlineData("stack")]
    [InlineData("unsupported")]
    public async Task Write_UnsupportedState_LeavesAllFieldsIntact(string state)
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                if (state == "held") fixture.Session.Set(ItemSessionKeys.Held, new HeldItem(note.Id));
                if (state == "stack") note.Amount = 2;
                if (state == "unsupported") note.TemplateId = "unrelated";
                Assert.False(
                    fixture.Books.Write(
                        note,
                        fixture.Other,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Other" }
                    )
                );
                Assert.Equal("Welcome Pippo", note.Name);
                Assert.Equal("Dear Pippo,\n\nBring this to Vega.", note.GetProp<string>("book.content"));
            }
        );
    }

    [Fact]
    public async Task Write_ValidItem_ChangesAllFieldsForSpecifiedRecipient()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                Assert.True(
                    fixture.Books.Write(
                        note,
                        fixture.Other,
                        "welcome_letter",
                        new Dictionary<string, object?> { ["contact_name"] = "Aria" }
                    )
                );
                Assert.Equal("Welcome Bruno", note.Name);
                Assert.Equal("Dear Bruno,\n\nBring this to Aria.", note.GetProp<string>("book.content"));
            }
        );
    }

    [Fact]
    public async Task Open_AfterRenameTransferAndSourceEdit_ReadsSavedText()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                fixture.Player.Name = "Aria";
                fixture.Source.Content = "Changed $player_name";
                fixture.Items.PlaceOnGround(note, MapType.Trammel, new(1600, 1600, 0));
                Assert.True(fixture.Books.Open(note, fixture.Other));
                Assert.Contains(
                    "Dear Pippo,<br><br>Bring this to Vega.",
                    Assert.Single(fixture.Gumps.Opened).Gump.Layout.Build().Strings
                );
            }
        );
    }

    [Theory]
    [InlineData("own", true)]
    [InlineData("ground", true)]
    [InlineData("chest", true)]
    [InlineData("other", false)]
    [InlineData("bank", false)]
    [InlineData("held", false)]
    [InlineData("distant", false)]
    [InlineData("map", false)]
    public async Task Open_NormalItemAccess_RespectsOwnerAndGroundRoot(string place, bool expected)
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                if (place == "held") fixture.Session.Set(ItemSessionKeys.Held, new HeldItem(note.Id));
                if (place is "other" or "bank")
                {
                    var pack = new ItemEntity { Id = new(0x40000005), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
                    pack.Equip(
                        place == "other" ? fixture.Other.Id : fixture.Player.Id,
                        place == "bank" ? LayerType.Bank : LayerType.Backpack
                    );
                    fixture.Items.Add([pack]);
                    fixture.Items.MoveToContainer(note, pack.Id, new Point2D(1, 1));
                }

                if (place is "ground" or "distant" or "map")
                {
                    fixture.Items.PlaceOnGround(
                        note,
                        place == "map" ? MapType.Felucca : MapType.Trammel,
                        new(place == "distant" ? 1610 : 1600, 1600, 0)
                    );
                }

                if (place == "chest")
                {
                    var chest = new ItemEntity { Id = new(0x40000006), TemplateId = "backpack", ItemId = 0x0E75 };
                    chest.PlaceOnGround(MapType.Trammel, new(1601, 1600, 0));
                    fixture.Items.Add([chest]);
                    fixture.Items.MoveToContainer(note, chest.Id, new Point2D(10, 10));
                }

                Assert.Equal(expected, fixture.Books.Open(note, fixture.Player));
                Assert.Equal(expected ? 1 : 0, fixture.Gumps.Opened.Count);
            }
        );
    }

    [Fact]
    public async Task Open_LegacyJailText_DisplaysLiteralMarkup()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                note.Props.Clear();
                note.SetProp("jail.text", "Old <note>\n$player_name");
                Assert.True(fixture.Books.Open(note, fixture.Player));
                Assert.Contains(
                    "Old &lt;note&gt;<br>$player_name",
                    Assert.Single(fixture.Gumps.Opened).Gump.Layout.Build().Strings
                );
            }
        );
    }

    [Fact]
    public async Task Open_NoteInsideAHeldBag_IsInaccessible()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
            {
                var note = fixture.Give();
                var bag = new ItemEntity { Id = new(0x40000009), TemplateId = "backpack", ItemId = 0x0E75 };
                bag.PutInContainer(fixture.Backpack.Id, new Point2D(10, 10));
                fixture.Items.Add([bag]);
                fixture.Items.MoveToContainer(note, bag.Id, new Point2D(10, 10));
                fixture.Session.Set(ItemSessionKeys.Held, new HeldItem(bag.Id));
                Assert.False(fixture.Books.Open(note, fixture.Player));
            }
        );
    }

    private static async Task<ItemEntity> BlankBookAsync(BookTestFixture f)
    {
        f.Data.With(
            f.Source,
            new BookTemplate
            {
                Id = "blank", Title = "a book", Author = "$player_name", Content = "", ItemTemplate = "readable_book",
                Writable = true, Pages = 30
            },
            new BookTemplate { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book" }
        );
        ItemEntity? blank = null;
        await f.OnLoopAsync(() => blank = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "blank")));

        return blank!;
    }
}
