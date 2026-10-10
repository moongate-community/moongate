using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Core.Geometry;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Weight;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class AddReagentsCommandTests : IAsyncLifetime
{
    private const string Pearl = "0x0f7a_black_pearl";
    private const string Moss = "0x0f7b_blood_moss";
    private const string Garlic = "0x0f84_garlic";

    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemHandlingService _handling = new();
    private readonly RecordingWorldViewService _view = new();
    private readonly StubWeightService _weight = new();
    private readonly RecordingFatigueService _fatigue = new();

    private readonly SpellCatalogService _catalog;
    private readonly ItemTemplateService _templates;

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;

    public AddReagentsCommandTests()
    {
        _templates = new(
            new StubDataLoaderService().With(
                new ItemTemplate { Id = Pearl, ItemId = new Serial(0x0F7A), Name = "black pearl" },
                new ItemTemplate { Id = Moss, ItemId = new Serial(0x0F7B), Name = "blood moss" },
                new ItemTemplate { Id = Garlic, ItemId = new Serial(0x0F84), Name = "garlic" }
            )
        );
        _catalog = new(
            new StubDataLoaderService().With(
                new SpellDefinition
                {
                    Id = 1, Key = "clumsy", Circle = 1,
                    Reagents = [new() { Template = Moss, Amount = 1 }, new() { Template = Garlic, Amount = 1 }]
                },
                new SpellDefinition
                {
                    Id = 2, Key = "create_food", Circle = 1,
                    Reagents = [new() { Template = Garlic, Amount = 1 }, new() { Template = Pearl, Amount = 2 }]
                },
                new SpellDefinition
                {
                    Id = 9, Key = "agility", Circle = 2, Reagents = [new() { Template = Moss, Amount = 1 }]
                }
            ),
            _templates
        );
    }

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var gm));
        (gm.Map, gm.Location) = (MapType.Felucca, new Point3D(150, 160, 7));
        _handling.Templates[Pearl] = 0x0F7A;
        _handling.Templates[Moss] = 0x0F7B;
        _handling.Templates[Garlic] = 0x0F84;
    }

    [Fact]
    public async Task ASpell_GivesItsReagents_TwentyOfEachByDefault()
    {
        var context = await RunAsync("clumsy");

        Assert.Equal([(Moss, 20), (Garlic, 20)], _handling.Given.Select(item => (item.TemplateId!, item.Amount)));
        Assert.Equal(
            "Reagents in your backpack, 20 of each: blood moss, garlic.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task AnAmount_IsGivenOfEachReagent()
    {
        await RunAsync("agility", "150");

        Assert.Equal([(Moss, 150)], _handling.Given.Select(item => (item.TemplateId!, item.Amount)));
    }

    [Fact]
    public async Task ACircle_GivesEachReagentOnce()
    {
        await RunAsync("circle", "1", "5");

        Assert.Equal(
            [(Moss, 5), (Garlic, 5), (Pearl, 5)],
            _handling.Given.Select(item => (item.TemplateId!, item.Amount))
        );
    }

    [Fact]
    public async Task All_GivesEveryReagentOfTheCatalog()
    {
        await RunAsync("all");

        Assert.Equal([Moss, Garlic, Pearl], _handling.Given.Select(item => item.TemplateId));
    }

    [Fact]
    public async Task ANumber_IsASpell()
    {
        await RunAsync("9");

        Assert.Equal([Moss], _handling.Given.Select(item => item.TemplateId));
    }

    [Fact]
    public async Task AFullBackpack_LeavesTheStacksAtTheFeet()
    {
        _handling.BackpackFull = true;

        var context = await RunAsync("agility", "40");

        Assert.Empty(_handling.Given);
        Assert.True(_items.TryGet(new Serial(0x40000500), out var pile));
        Assert.Equal((Moss, 40), (pile.TemplateId, pile.Amount));
        Assert.Equal(
            (MapType.Felucca, new Point3D(150, 160, 7)),
            (pile.Map, pile.GroundLocation)
        );
        Assert.Equal([$"Appeared {pile.Id.Value}"], _view.Calls);
        Assert.Equal(
            "40 of each did not fit the backpack and lie at your feet: blood moss.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task AStackTooHeavyForTheBackpack_LiesAtTheFeet_AndNothingIsGiven()
    {
        var backpack = new ItemEntity { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(new Serial(2), LayerType.Backpack);
        _items.Add([backpack]);
        _weight.HoldsResult = false;

        var context = await RunAsync("agility", "40");

        Assert.Empty(_handling.Given);
        Assert.Single(_view.Calls);
        Assert.Empty(_fatigue.Loads);
        Assert.Equal(
            "40 of each did not fit the backpack and lie at your feet: blood moss.",
            Assert.Single(context.Output).Text
        );
    }

    [Fact]
    public async Task WhatIsGiven_ShowsTheNewWeightOnTheStatusBarOnce()
    {
        await RunAsync("clumsy");

        var load = Assert.Single(_fatigue.Loads);
        Assert.Equal((new Serial(2), false), (load.Mobile.Id, load.Warn));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1001")]
    [InlineData("-3")]
    [InlineData("many")]
    public async Task ABadAmount_SaysTheUsage(string amount)
    {
        var context = await RunAsync("clumsy", amount);

        Assert.StartsWith("Usage: add_reagents", Assert.Single(context.Output).Text);
        Assert.Empty(_handling.Given);
    }

    [Fact]
    public async Task AnUnknownSpell_IsSaid()
    {
        var context = await RunAsync("nothing");

        Assert.Equal("Unknown spell: nothing", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task AnUnknownCircle_IsSaid()
    {
        var context = await RunAsync("circle", "9");

        Assert.Equal("Unknown circle: 9. A circle is a number from 1 to 8.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task NoArguments_SaysTheUsage()
    {
        var context = await RunAsync();

        Assert.StartsWith("Usage: add_reagents", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("add_reagents all", "add_reagents", ["all"], CommandSourceType.Console, null);

        await Command(null).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        var context = await RunAsync(["agility"], TestLocalization.With((30251, "Reagenti, {0} ciascuno: {1}.")));

        Assert.Equal("Reagenti, 20 ciascuno: blood moss.", Assert.Single(context.Output).Text);
    }

    private Task<CommandContext> RunAsync(params string[] arguments)
    {
        return RunAsync(arguments, null);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization)
    {
        var context = new CommandContext(".add_reagents", "add_reagents", arguments, CommandSourceType.InGame, _session);

        await Command(localization).ExecuteAsync(context);

        return context;
    }

    private AddReagentsCommand Command(ILocalizationService? localization)
    {
        return new(_catalog, _templates, _handling, _items, _view, _fixture.Mobiles, _fixture.Network.Loop, localization, _weight, _fatigue);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
