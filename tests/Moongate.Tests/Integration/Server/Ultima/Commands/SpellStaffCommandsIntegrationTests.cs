using System.Numerics;
using Moongate.Core.Directories;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.World;

namespace Moongate.Tests.Integration.Server.Ultima.Commands;

/// <summary>
///     <c>.add_spell</c> and <c>.add_reagents</c> over the shipped <c>spells.toml</c> and item templates, with the real
///     spellbook service: the whole of Magery in a book, and the eight classic reagents in a backpack.
/// </summary>
public sealed class SpellStaffCommandsIntegrationTests : IAsyncLifetime
{
    private readonly StubTargetService _targets = new();
    private readonly StubItemHandlingService _handling = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly RecordingSpeechService _speech = new();

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private ItemService _items = null!;
    private SpellCatalogService _catalog = null!;
    private ItemTemplateService _templates = null!;
    private SpellbookService _books = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        _items = TestItems.Create(_fixture.Sectors);
        var directories = new DirectoriesConfig(Path.Combine(RepositoryRoot(), "moongate_root"), ["data", "templates"]);
        var shipped = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        _templates = new(new StubDataLoaderService().With(shipped));
        var spells = (await new SpellsLoader(directories, new StubDataLoaderService().With(shipped)).LoadDataAsync())
            .Entities.ToArray();
        _catalog = new(new StubDataLoaderService().With(spells), _templates);
        _books = new(_items, _templates, _catalog, _fixture.Sender, _speech, _handling, _fixture.Sessions);

        foreach (var template in shipped.Where(template => template.ItemId.Value > 0))
        {
            _handling.Templates[template.Id] = (int)template.ItemId.Value;
        }
    }

    [Fact]
    public async Task AddSpellAll_FillsAnEmptyBookWithTheSixtyFourSpells()
    {
        var book = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "spellbook", ItemId = 0x0EFA, Amount = 1 };
        _items.Add([book]);
        _targets.Result = TargetResult.ForObject(book.Id);
        var context = Context("add_spell", "all");

        await new AddSpellCommand(_targets, _catalog, _books, _items, _fixture.Mobiles, _fixture.Network.Loop)
            .ExecuteAsync(context);

        Assert.Equal(64, BitOperations.PopCount(_books.GetSpells(book)));
        Assert.Equal(ulong.MaxValue, _books.GetSpells(book));
        Assert.Equal(
            "Spells added to the spellbook: 64 new, 64 in the book now.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task AddReagentsAll_GivesTheEightClassicReagents()
    {
        var context = Context("add_reagents", "all");

        await new AddReagentsCommand(
                _catalog,
                _templates,
                _handling,
                _items,
                _view,
                _fixture.Mobiles,
                _fixture.Network.Loop
            )
            .ExecuteAsync(context);

        Assert.Equal(
            [
                "0x0f7a_black_pearl", "0x0f7b_blood_moss", "0x0f84_garlic", "0x0f85_ginseng", "0x0f86_mandrake_root",
                "0x0f88_nightshade", "0x0f8c_sulfurous_ash", "0x0f8d_spider_silk"
            ],
            _handling.Given.Select(item => item.TemplateId).Order()
        );
        Assert.All(_handling.Given, item => Assert.Equal(20, item.Amount));
    }

    private CommandContext Context(string name, params string[] arguments)
    {
        return new("." + name, name, arguments, CommandSourceType.InGame, _session);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Moongate.slnx not found above the test output.");
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
