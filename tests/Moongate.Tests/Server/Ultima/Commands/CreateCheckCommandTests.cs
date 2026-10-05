using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class CreateCheckCommandTests : IAsyncLifetime
{
    private readonly ItemService _items = TestItems.Create();
    private readonly StubItemSerialPool _serials = new();
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "backpack", ItemId = new Serial(0x0E75) },
            new ItemTemplate { Id = BankService.CheckTemplate, ItemId = new Serial(0x14F0), Name = "bank check" }
        )
    );

    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private ItemHandlingService _handling = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        var factory = new FakeItemFactoryService(_templates, new FakeTileDataService());
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

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task AnAmount_PutsACheckWorthItInTheBackpack()
    {
        var backpack = Backpack();

        var context = await RunAsync(["2000"]);

        var check = Assert.Single(_items.GetContents(backpack.Id));
        Assert.Equal(BankService.CheckTemplate, check.TemplateId);
        Assert.Equal(2000, BankService.CheckWorth(check));
        Assert.True(check.TryGetProp<int>(ItemPropKeys.LabelNumber, out var label));
        Assert.Equal(BankService.CheckLabel, label);
        Assert.Equal("A bank check worth 2,000 gold is in your backpack.", Assert.Single(context.Output).Text);
    }

    // Staff is not held to the banker's smallest and largest check.
    [Theory]
    [InlineData("1")]
    [InlineData("2000000000")]
    public async Task AnyAmountABankHolds_IsWritten(string amount)
    {
        var backpack = Backpack();

        await RunAsync([amount]);

        Assert.Equal(long.Parse(amount), BankService.CheckWorth(Assert.Single(_items.GetContents(backpack.Id))));
    }

    [Theory]
    [InlineData]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("2000000001")]
    [InlineData("lots")]
    [InlineData("5,000")]
    [InlineData("10", "20")]
    public async Task ABadAmount_SaysTheUsage_AndMakesNothing(params string[] arguments)
    {
        var backpack = Backpack();

        var context = await RunAsync(arguments);

        Assert.Equal("Usage: create_check <1..2000000000>", Assert.Single(context.Output).Text);
        Assert.Empty(_items.GetContents(backpack.Id));
    }

    [Fact]
    public async Task WithNoBackpack_NothingIsMade_AndTheSerialIsNotUsed()
    {
        var context = await RunAsync(["2000"]);

        Assert.Equal("No check was made: you have no backpack, or it is full.", Assert.Single(context.Output).Text);
        Assert.Single(_serials.Serials);
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        var context = new CommandContext("create_check 2000", "create_check", ["2000"], CommandSourceType.Console, null);

        await Command(null).ExecuteAsync(context);

        Assert.Equal("create_check works in game only.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        Backpack();

        var context = await RunAsync(["2000"], TestLocalization.With((30175, "Nel tuo zaino c'è un assegno da {0} monete.")));

        Assert.Equal("Nel tuo zaino c'è un assegno da 2,000 monete.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(string[] arguments, ILocalizationService? localization = null)
    {
        var context = new CommandContext(".create_check", "create_check", arguments, CommandSourceType.InGame, _session);

        await Command(localization).ExecuteAsync(context);

        return context;
    }

    private CreateCheckCommand Command(ILocalizationService? localization)
    {
        return new(_handling, _fixture.Mobiles, _fixture.Network.Loop, localization);
    }

    private ItemEntity Backpack()
    {
        var backpack = new ItemEntity { Id = new Serial(0x40001000), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
        backpack.Equip(new Serial(2), LayerType.Backpack);
        _items.Add([backpack]);

        return backpack;
    }
}
