using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class AddGoldCommandTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemSerialPool _serials = new();
    private readonly StubTargetService _targets = new();
    private readonly RecordingFatigueService _fatigue = new();

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
            new ItemTemplate { Id = "gold", ItemId = new Serial(0x0EED), Weight = 0.02m }
        )
    );

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private ItemHandlingService _handling = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        await _fixture.AddAsync(3);
        var factory = new FakeItemFactoryService(
            _templates,
            new FakeTileDataService().Item(0x0EED, TileFlagType.Generic, 0)
        );
        _handling = new(
            _items,
            _fixture.Sessions,
            _fixture.Sender,
            new RecordingWorldViewService(),
            TestTooltips.Create(_items, _fixture.Mobiles),
            factory,
            _serials,
            null,
            new ContainerCapacityService(_items, _templates, new BankConfig())
        );
        _serials.Serials.Enqueue(new Serial(0x40002000));
    }

    [Fact]
    public async Task AnAmount_ThenAMobile_PutsThatGoldInItsBackpack()
    {
        var backpack = Backpack(3);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out var bob));
        bob.Name = "Bob";
        _targets.Result = TargetResult.ForObject(new Serial(3));

        var context = await RunAsync(["1000"]);

        var pile = Assert.Single(_items.GetContents(backpack.Id));
        Assert.Equal(("gold", 1000), (pile.TemplateId, pile.Amount));
        Assert.Equal("1,000 gold is in the backpack of Bob.", Assert.Single(context.Output).Text);
        // Gold weighs: the one who got it sees its new load.
        Assert.Equal((bob, false), Assert.Single(_fatigue.Loads));
    }

    [Fact]
    public async Task TheGameMasterItself_MayBeTheTarget()
    {
        var backpack = Backpack(2);
        _targets.Result = TargetResult.ForObject(new Serial(2));

        await RunAsync(["60000"]);

        Assert.Equal(60_000, Assert.Single(_items.GetContents(backpack.Id)).Amount);
    }

    [Theory]
    [InlineData]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("60001")]
    [InlineData("lots")]
    [InlineData("10", "20")]
    public async Task ABadAmount_SaysTheUsage_AndOpensNoCursor(params string[] arguments)
    {
        var context = await RunAsync(arguments);

        Assert.Equal("Usage: add_gold <1..60000>", Assert.Single(context.Output).Text);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task ACanceledCursor_AddsNothing()
    {
        var backpack = Backpack(2);

        var context = await RunAsync(["1000"]);

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.Empty(_items.GetContents(backpack.Id));
    }

    [Fact]
    public async Task AnItem_IsNotAMobile()
    {
        var backpack = Backpack(2);
        _targets.Result = TargetResult.ForObject(backpack.Id);

        var context = await RunAsync(["1000"]);

        Assert.Equal("That is not a character or an NPC.", Assert.Single(context.Output).Text);
        Assert.Empty(_items.GetContents(backpack.Id));
    }

    [Fact]
    public async Task AMobileWithNoBackpack_GetsNothing_AndTheSerialIsNotUsed()
    {
        Assert.True(_fixture.Mobiles.TryGet(new Serial(3), out var bob));
        bob.Name = "Bob";
        _targets.Result = TargetResult.ForObject(new Serial(3));

        var context = await RunAsync(["1000"]);

        Assert.Equal("Bob has no backpack, or it is full.", Assert.Single(context.Output).Text);
        Assert.Single(_serials.Serials);
        Assert.Empty(_fatigue.Loads);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("add_gold 1000", "add_gold", ["1000"], CommandSourceType.Console, null);

        await Command(null).ExecuteAsync(context);

        Assert.Equal("add_gold works in game only.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        Backpack(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out var aria));
        aria.Name = "Aria";
        _targets.Result = TargetResult.ForObject(new Serial(2));

        var context = await RunAsync(["1000"], TestLocalization.With((30178, "{1} monete nello zaino di {0}.")));

        Assert.Equal("1,000 monete nello zaino di Aria.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization = null)
    {
        var context = new CommandContext(".add_gold", "add_gold", arguments, CommandSourceType.InGame, _session);

        await Command(localization).ExecuteAsync(context);

        return context;
    }

    private AddGoldCommand Command(ILocalizationService? localization)
    {
        return new(
            _targets,
            _handling,
            _fixture.Mobiles,
            _fixture.Sessions,
            _fixture.Network.Loop,
            new ItemsConfig { GoldTemplate = "gold", BackpackTemplate = "backpack" },
            _fatigue,
            localization
        );
    }

    private ItemEntity Backpack(uint owner)
    {
        var backpack = new ItemEntity
            { Id = new Serial(0x40001000 + owner), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(new Serial(owner), LayerType.Backpack);
        _items.Add([backpack]);

        return backpack;
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
