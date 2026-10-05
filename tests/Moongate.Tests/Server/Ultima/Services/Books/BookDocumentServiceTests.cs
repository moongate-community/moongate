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
