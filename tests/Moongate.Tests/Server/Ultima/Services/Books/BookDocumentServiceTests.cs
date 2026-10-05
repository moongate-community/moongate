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
            new BookTemplate { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book", ItemId = 0x0FF2 },
            new BookTemplate { Id = "plain", Title = "Plain", Content = "Text", ItemTemplate = "readable_book" });

        await f.OnLoopAsync(() =>
        {
            var tome = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "tome"));
            f.Serials.Serials.Enqueue(new(0x40000F01));
            var plain = Assert.IsType<ItemEntity>(f.Books.Give(f.Player, "plain"));

            Assert.Equal(("readable_book", 0x0FF2), (tome.TemplateId, tome.ItemId));
            Assert.Equal(0x0FF1, plain.ItemId);
        });
    }

    // A book item opens the client's book: its cover, then every page; no parchment.
    [Fact]
    public async Task Open_ABook_SendsItsHeaderAndItsPages_AndOpensNoGump()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(new BookTemplate { Id = "tome", Title = "Tome", Author = "Yorick", Content = "one\ntwo\n\nthree", ItemTemplate = "readable_book" });

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
        });
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
        });
    }

    // Written on a scroll from a source that has a cover: the scroll takes the graphic and stays a parchment.
    [Fact]
    public async Task Open_AScrollWrittenFromABookSource_IsStillAParchment()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(f.Source, new BookTemplate { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book", ItemId = 0x0FF2 });

        await f.OnLoopAsync(() =>
        {
            var letter = f.Give();
            Assert.True(f.Books.Write(letter, f.Player, "tome"));
            f.World.Sender.Sent.Clear();

            Assert.True(f.Books.Open(letter, f.Player));

            Assert.Single(f.Gumps.Opened);
            Assert.DoesNotContain(f.World.Sender.Sent, packet => packet is BookHeaderPacket);
        });
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
        });
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
        });
    }

    [Fact]
    public async Task Write_ASourceWithAnItemId_SetsTheGraphicOfTheItemWrittenOn()
    {
        await using var f = await BookTestFixture.CreateAsync();
        f.Data.With(f.Source, new BookTemplate { Id = "tome", Title = "Tome", Content = "Text", ItemTemplate = "readable_book", ItemId = 0x0FF2 });

        await f.OnLoopAsync(() =>
        {
            var letter = f.Give();

            Assert.True(f.Books.Write(letter, f.Player, "tome"));
            Assert.Equal(0x0FF2, letter.ItemId);
        });
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
        });
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
        });
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.OnLoopAsync(() =>
        {
            Assert.Equal(Moongate.Server.Ultima.Types.Bank.BankResultType.Busy, f.Books.Bank.Withdraw(f.Books.Player, 1));
            Assert.Equal(Moongate.Server.Ultima.Types.Bank.BankResultType.Busy, f.Books.Bank.Deposit(f.Books.Player, 1));
            Assert.Equal((200, 10), (bankGold.Amount, carriedGold.Amount));
        });
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
        });
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.OnLoopAsync(() =>
        {
            var serials = f.Books.Serials.Serials.Count;
            if (cash)
            {
                Assert.Equal(Moongate.Server.Ultima.Types.Bank.BankResultType.Busy,
                    f.Books.Bank.Cash(f.Books.Player, check, out var deposited));
                Assert.Equal(0, deposited);
            }
            else
            {
                Assert.Equal(Moongate.Server.Ultima.Types.Bank.BankResultType.Busy,
                    f.Books.Bank.WriteCheck(f.Books.Player, 5000));
            }
            Assert.Equal(20000, gold.Amount);
            Assert.Equal(5000L, check.GetProp<long>(ItemPropKeys.BankWorth));
            Assert.True(f.Books.Items.TryGet(check.Id, out _));
            Assert.Equal(serials, f.Books.Serials.Serials.Count);
        });
        f.Store.Continue.TrySetResult();
        await pending;
    }

    [Fact]
    public async Task Give_AttachmentsFreezeWithoutCreatingRewardItemsOrChangingLetterWeight()
    {
        await using var f = await BookTestFixture.CreateAsync();
        await f.OnLoopAsync(() =>
        {
            f.Source.Attachments.Add(new() { ItemTemplate = "gold", Amount = DiceSpec.Parse("100"), Hue = HueSpec.FromValue(42) });
            var letter = f.Give();
            Assert.Equal(2, f.Items.Items.Count);
            Assert.Equal(1m, new WeightService(f.Items, f.ItemTemplates, new FakeTileDataService().Item(0x14ED, TileFlagType.None, 1)).Of(letter));
            var payload = Assert.IsType<string>(letter.Props!["book.attachments"]);
            Assert.True(BookAttachmentCodec.TryDecode(payload, out var batch));
            Assert.Equal(100, Assert.Single(batch!.Items).Amount);
            Assert.Equal(42, Assert.Single(batch.Items).Hue);
            f.Source.Attachments[0].Amount = DiceSpec.Parse("200");
            Assert.Equal(payload, letter.GetProp<string>("book.attachments"));
        });
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
            Assert.False(f.Books.Write(plain, f.Player, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Vega" }));
            Assert.Equal("Dear Pippo,\n\nBring this to Vega.", plain.GetProp<string>("book.content"));
            f.Serials.Serials.Enqueue(new(0x40001000));
            var gift = f.Give();
            var original = gift.GetProp<string>("book.attachments");
            f.Source.Attachments[0].Amount = DiceSpec.Parse("999");
            Assert.True(f.Books.Write(gift, f.Other, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Vega" }));
            Assert.Equal(original, gift.GetProp<string>("book.attachments"));
        });
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
            Assert.False(f.Books.Write(letter, f.Player, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Vega" }));
            Assert.Equal("Dear Pippo,\n\nBring this to Vega.", letter.GetProp<string>("book.content"));
        });
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
            Assert.True(f.Books.Books.Write(f.Letter, f.Books.Player, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Vega" }));
            Assert.Equal(payload, f.Letter.GetProp<string>(BookAttachmentCodec.PropKey));
            Assert.False(f.Service.CanClaim(f.Letter, f.Books.Session));
            Assert.Equal(3, f.Books.Items.Items.Count);
        });
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
            Assert.False(f.Books.Write(letter, f.Player, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Vega" }));
            Assert.Equal("Dear Pippo,\n\nBring this to Vega.", letter.GetProp<string>("book.content"));
        });
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
        });
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
        });
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
            Assert.False(fixture.Books.Write(note, fixture.Other, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Other" }));
            Assert.Equal("Welcome Pippo", note.Name);
            Assert.Equal("Dear Pippo,\n\nBring this to Vega.", note.GetProp<string>("book.content"));
        });
    }

    [Fact]
    public async Task Write_ValidItem_ChangesAllFieldsForSpecifiedRecipient()
    {
        await using var fixture = await BookTestFixture.CreateAsync();
        await fixture.OnLoopAsync(() =>
        {
            var note = fixture.Give();
            Assert.True(fixture.Books.Write(note, fixture.Other, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Aria" }));
            Assert.Equal("Welcome Bruno", note.Name);
            Assert.Equal("Dear Bruno,\n\nBring this to Aria.", note.GetProp<string>("book.content"));
        });
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
            Assert.Contains("Dear Pippo,<br><br>Bring this to Vega.", Assert.Single(fixture.Gumps.Opened).Gump.Layout.Build().Strings);
        });
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
                pack.Equip(place == "other" ? fixture.Other.Id : fixture.Player.Id, place == "bank" ? LayerType.Bank : LayerType.Backpack);
                fixture.Items.Add([pack]);
                fixture.Items.MoveToContainer(note, pack.Id, new Point2D(1, 1));
            }
            if (place is "ground" or "distant" or "map")
            {
                fixture.Items.PlaceOnGround(note, place == "map" ? MapType.Felucca : MapType.Trammel, new(place == "distant" ? 1610 : 1600, 1600, 0));
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
        });
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
            Assert.Contains("Old &lt;note&gt;<br>$player_name", Assert.Single(fixture.Gumps.Opened).Gump.Layout.Build().Strings);
        });
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
        });
    }
}
