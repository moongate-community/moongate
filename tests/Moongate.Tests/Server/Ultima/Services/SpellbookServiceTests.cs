using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SpellbookServiceTests : IAsyncLifetime
{
    private const int ClumsyScroll = 0x1F2E;
    private const int HealScroll = 0x1F31;

    private readonly RecordingSpeechService _speech = new();
    private readonly StubItemHandlingService _handling = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;
    private ItemService _items = null!;
    private ItemEntity _backpack = null!;
    private SpellbookService _books = null!;
    private uint _next = 0x40000010;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _items = TestItems.Create(_fixture.Sectors);
        _backpack = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        _backpack.Equip(_aria.Id, LayerType.Backpack);
        _items.Add([_backpack]);
        var templates = new ItemTemplateService(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = "clumsyscroll", ItemId = new Serial(ClumsyScroll) },
                new ItemTemplate { Id = "healscroll", ItemId = new Serial(HealScroll) },
                new ItemTemplate
                {
                    Id = "spellbook1", ItemId = new Serial(0x0EFA), Tags = new() { ["spells"] = "255" }
                }
            )
        );
        var catalog = new SpellCatalogService(
            new StubDataLoaderService().With(
                new SpellDefinition { Id = 1, Key = "clumsy", Scroll = "clumsyscroll" },
                new SpellDefinition { Id = 4, Key = "heal", Scroll = "healscroll" }
            ),
            templates
        );
        _books = new(_items, templates, catalog, _fixture.Sender, _speech, _handling);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void IsSpellbook_IsTheGraphic()
    {
        Assert.True(_books.IsSpellbook(Book("spellbook")));
        Assert.False(_books.IsSpellbook(Scroll(ClumsyScroll)));
    }

    [Fact]
    public void ABookWithNoPropHoldsWhatItsTemplateSays_AndAnEmptyOneNothing()
    {
        Assert.Equal(255UL, _books.GetSpells(Book("spellbook1")));
        Assert.Equal(0UL, _books.GetSpells(Book("spellbook")));
    }

    [Fact]
    public void Add_PutsTheBit_ShowsTheBook_AndRefusesASecondTime()
    {
        var book = Book("spellbook1");

        Assert.False(_books.Add(book, 1));
        Assert.True(_books.Add(Book("spellbook"), 4));
        var empty = Book("spellbook");
        Assert.True(_books.Add(empty, 4));

        Assert.True(_books.Has(empty, 4));
        Assert.False(_books.Has(empty, 5));
        Assert.Equal(8UL, _books.GetSpells(empty));
        Assert.Contains("spellbook", _handling.Refreshed);
        Assert.False(_books.Add(empty, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void Add_ANumberOutsideTheBook_IsRefused(int spell)
    {
        Assert.False(_books.Add(Book("spellbook"), spell));
        Assert.False(_books.Has(Book("spellbook1"), spell));
    }

    [Fact]
    public void Add_TheSixtyFourthSpell_KeepsTheTopBit()
    {
        var book = Book("spellbook");

        Assert.True(_books.Add(book, 64));

        Assert.Equal(1UL << 63, _books.GetSpells(book));
        Assert.True(_books.Has(book, 64));
    }

    [Fact]
    public void FindCarried_PrefersTheWornBook_ThenTheBackpackOne_AndNeedsTheSpell()
    {
        var carried = Book("spellbook1", _backpack.Id);
        var worn = Book("spellbook");
        worn.Equip(_aria.Id, LayerType.OneHanded);
        _items.Add([worn]);
        _books.Add(worn, 4);

        Assert.Equal(worn, _books.FindCarried(_aria, 4));
        Assert.Equal(carried, _books.FindCarried(_aria, 1));
        Assert.Null(_books.FindCarried(_aria, 20));
        Assert.NotNull(_books.FindCarried(_aria, 0));
    }

    [Fact]
    public void Open_SendsTheGumpAndTheSpellsAsFakeItemsWhoseAmountIsTheNumber()
    {
        var book = Book("spellbook");
        _books.Add(book, 1);
        _books.Add(book, 4);
        _books.Add(book, 64);

        _books.Open(_session, book);

        var display = Assert.Single(_fixture.Sender.Sent.OfType<DisplayContainerPacket>());
        Assert.Equal((book.Id, 0xFFFF), (display.Container, display.Gump));
        var content = Assert.Single(_fixture.Sender.Sent.OfType<ContainerContentPacket>());
        Assert.Equal([1, 4, 64], content.Items.Select(entry => entry.Amount));
        Assert.Equal([0x7FFFFFFFu, 0x7FFFFFFCu, 0x7FFFFFC0u], content.Items.Select(entry => entry.Serial.Value));
        Assert.All(content.Items, entry => Assert.Equal(book.Id, entry.Container));
    }

    [Fact]
    public void TryOpen_ABookInTheBackpackOrWorn_Opens_AnotherPlace_TellsItMustBeCarried()
    {
        var pocket = Book("spellbook1", _backpack.Id);
        var bag = new ItemEntity { Id = new Serial(0x40000002), TemplateId = "bag", ItemId = 0x0E76, Amount = 1 };
        bag.PutInContainer(_backpack.Id, new Point2D(1, 1));
        _items.Add([bag]);
        var deep = Book("spellbook1", bag.Id);

        Assert.True(_books.TryOpen(_session, _aria, pocket));
        Assert.False(_books.TryOpen(_session, _aria, deep));

        Assert.Equal(
            ISpellbookService.MustBeCarriedMessage,
            Assert.Single(_speech.ToldClilocs, told => told.Player == _aria).Cliloc
        );
    }

    [Fact]
    public void AddScroll_AddsTheSpell_UsesUpOneScroll_AndPlaysTheSound()
    {
        var book = Book("spellbook", _backpack.Id);
        var scroll = Scroll(ClumsyScroll, 3);

        var result = _books.AddScroll(_aria, book, scroll);

        Assert.Equal(SpellbookDropType.Added, result);
        Assert.True(_books.Has(book, 1));
        Assert.Equal((scroll, 1), Assert.Single(_handling.Consumed));
        Assert.Equal(2, scroll.Amount);
        Assert.Equal(0x249, Assert.Single(_speech.PlacedSounds).Sound);
    }

    [Fact]
    public void AddScroll_ASpellTheBookHoldsAlready_IsRefusedWithAMessage()
    {
        var book = Book("spellbook1", _backpack.Id);
        var scroll = Scroll(ClumsyScroll);

        var result = _books.AddScroll(_aria, book, scroll);

        Assert.Equal(SpellbookDropType.AlreadyPresent, result);
        Assert.Empty(_handling.Consumed);
        Assert.Equal(500179, Assert.Single(_speech.ToldClilocs).Cliloc);
    }

    [Fact]
    public void AddScroll_ANonScroll_OrABookThePlayerDoesNotCarry_IsIgnored()
    {
        var book = Book("spellbook", _backpack.Id);
        var stranger = Book("spellbook");
        var plain = Scroll(0x0E34);

        Assert.Equal(SpellbookDropType.Ignored, _books.AddScroll(_aria, book, plain));
        Assert.Equal(SpellbookDropType.Ignored, _books.AddScroll(_aria, stranger, Scroll(ClumsyScroll)));
        Assert.Empty(_handling.Consumed);
        Assert.Empty(_speech.ToldClilocs);
    }

    private ItemEntity Book(string template, Serial? container = null)
    {
        var book = new ItemEntity { Id = new Serial(_next++), TemplateId = template, ItemId = 0x0EFA, Amount = 1 };

        if (container is { } holder)
        {
            book.PutInContainer(holder, new Point2D(1, 1));
        }

        _items.Add([book]);

        return book;
    }

    private ItemEntity Scroll(int graphic, int amount = 1)
    {
        var scroll = new ItemEntity { Id = new Serial(_next++), TemplateId = "scroll", ItemId = graphic, Amount = amount };
        scroll.PutInContainer(_backpack.Id, new Point2D(2, 2));
        _items.Add([scroll]);

        return scroll;
    }
}
