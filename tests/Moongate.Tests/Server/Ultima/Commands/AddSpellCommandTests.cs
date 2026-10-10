using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Magic;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class AddSpellCommandTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly StubTargetService _targets = new();
    private readonly StubSpellbookService _books = new();

    private readonly SpellCatalogService _catalog = new(
        new StubDataLoaderService().With(
            new SpellDefinition { Id = 1, Key = "clumsy", Circle = 1, Scroll = "none" },
            new SpellDefinition { Id = 2, Key = "create_food", Circle = 1, Scroll = "none" },
            new SpellDefinition { Id = 9, Key = "agility", Circle = 2, Scroll = "none" },
            new SpellDefinition { Id = 10, Key = "cunning", Circle = 2, Scroll = "none" },
            new SpellDefinition { Id = 17, Key = "bless", Circle = 3, Scroll = "none" }
        ),
        new ItemTemplateService(new StubDataLoaderService())
    );

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private ItemEntity _book = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        _book = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _items.Add([_book]);
        _targets.Result = TargetResult.ForObject(_book.Id);
    }

    [Fact]
    public async Task AKey_AddsThatSpell_AndSaysHowManyWereNew()
    {
        var context = await RunAsync("clumsy");

        Assert.Equal([1], _books.Books[_book.Id.Value].Order());
        Assert.Equal("Spells added to the spellbook: 1 new, 1 in the book now.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ANumber_AddsThatSpell()
    {
        await RunAsync("10");

        Assert.Equal([10], _books.Books[_book.Id.Value].Order());
    }

    [Fact]
    public async Task ACircle_AddsEverySpellOfThatCircle()
    {
        var context = await RunAsync("circle", "2");

        Assert.Equal([9, 10], _books.Books[_book.Id.Value].Order());
        Assert.Equal("Spells added to the spellbook: 2 new, 2 in the book now.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task All_AddsEverySpellOfTheCatalog_AndCountsOnlyTheNewOnes()
    {
        _books.Add(_book, 1);

        var context = await RunAsync("all");

        Assert.Equal([1, 2, 9, 10, 17], _books.Books[_book.Id.Value].Order());
        Assert.Equal("Spells added to the spellbook: 4 new, 5 in the book now.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ASpellTheBookHolds_IsZeroNew()
    {
        _books.Add(_book, 1);

        var context = await RunAsync("clumsy");

        Assert.Equal("Spells added to the spellbook: 0 new, 1 in the book now.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task AMobile_GetsTheSpellsInTheBookItCarries()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out var bob));
        _books.Carried.Add(_book);
        _targets.Result = TargetResult.ForObject(new Serial(3));

        await RunAsync("bless");

        Assert.Equal([17], _books.Books[_book.Id.Value].Order());
        Assert.NotNull(bob);
    }

    [Fact]
    public async Task AMobileWithNoBook_IsToldSo()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out var bob));
        bob.Name = "Bob";
        _targets.Result = TargetResult.ForObject(new Serial(3));

        var context = await RunAsync("all");

        Assert.Equal("Bob carries no spellbook.", Assert.Single(context.Output).Text);
        Assert.Empty(_books.Books);
    }

    [Fact]
    public async Task AnItemThatIsNeitherABookNorAMobile_IsRefused()
    {
        var stone = new ItemEntity { Id = new Serial(0x40000011), TemplateId = "stone", ItemId = 0x1363, Amount = 1 };
        _items.Add([stone]);
        _targets.Result = TargetResult.ForObject(stone.Id);

        var context = await RunAsync("all");

        Assert.Equal("That is not a spellbook, a character or an NPC.", Assert.Single(context.Output).Text);
        Assert.Empty(_books.Books);
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("65")]
    [InlineData("0")]
    public async Task AnUnknownSpell_IsSaid_AndOpensNoCursor(string spell)
    {
        var context = await RunAsync(spell);

        Assert.Equal($"Unknown spell: {spell}", Assert.Single(context.Output).Text);
        Assert.Equal(0, _targets.Requests);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("0")]
    [InlineData("x")]
    public async Task AnUnknownCircle_IsSaid_AndOpensNoCursor(string circle)
    {
        var context = await RunAsync("circle", circle);

        Assert.Equal(
            $"Unknown circle: {circle}. A circle is a number from 1 to 8.",
            Assert.Single(context.Output).Text
        );
        Assert.Equal(0, _targets.Requests);
    }

    [Theory]
    [InlineData]
    [InlineData("circle")]
    [InlineData("all", "extra")]
    public async Task ABadShape_SaysTheUsage_AndOpensNoCursor(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.StartsWith("Usage: add_spell", Assert.Single(context.Output).Text);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ACanceledCursor_AddsNothing()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Felucca, new Point3D(1, 1, 0));

        var context = await RunAsync("all");

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_books.Books);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("add_spell all", "add_spell", ["all"], CommandSourceType.Console, null);

        await Command(null).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(
            ["clumsy"],
            TestLocalization.With((30245, "Incantesimi: {0} nuovi, {1} nel libro."))
        );

        Assert.Equal("Incantesimi: 1 nuovi, 1 nel libro.", Assert.Single(context.Output).Text);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization)
    {
        var context = new CommandContext(".add_spell", "add_spell", arguments, CommandSourceType.InGame, _session);

        await Command(localization).ExecuteAsync(context);

        return context;
    }

    private AddSpellCommand Command(ILocalizationService? localization)
    {
        return new(_targets, _catalog, _books, _items, _fixture.Mobiles, _fixture.Network.Loop, localization);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
