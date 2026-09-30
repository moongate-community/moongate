using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class LockUnlockCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "decoration_door", ItemId = new Serial(0x0675), ScriptId = "door" },
            new ItemTemplate { Id = "decoration", ItemId = new Serial(0x0A28) }
        )
    );
    private readonly ItemEntity _left = Door(0x40000010, 1600);
    private readonly ItemEntity _right = Door(0x40000011, 1601);
    private readonly ItemEntity _candle = new() { Id = new Serial(0x40000012), TemplateId = "decoration", ItemId = 0x0A28, Amount = 1 };

    private SessionFixture? _fixture;

    public LockUnlockCommandTests()
    {
        _left.Props!["door.link"] = (long)_right.Id.Value;
        _right.Props!["door.link"] = (long)_left.Id.Value;
        _candle.PlaceOnGround(MapType.Trammel, new Point3D(1602, 1600, 0));
        _items.Add([_left, _right, _candle]);
    }

    [Fact]
    public async Task Lock_ADoor_LocksItAndItsLinkedDoor()
    {
        _targets.Result = TargetResult.ForObject(_left.Id);

        var context = await RunAsync("lock");

        Assert.Equal((true, true), (_left.Props!["locked"], _right.Props!["locked"]));
        Assert.Equal("The door is now locked.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task Unlock_ALockedDoor_UnlocksItAndItsLinkedDoor()
    {
        _left.Props!["locked"] = true;
        _right.Props!["locked"] = true;
        _targets.Result = TargetResult.ForObject(_right.Id);

        var context = await RunAsync("unlock");

        Assert.False(_left.Props!.ContainsKey("locked"));
        Assert.False(_right.Props!.ContainsKey("locked"));
        Assert.Equal("The door is now unlocked.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task AnItemThatIsNotADoor_OrAMobile_IsRefused()
    {
        _targets.Result = TargetResult.ForObject(_candle.Id);

        var context = await RunAsync("lock");

        Assert.Equal("That is not a door.", Assert.Single(context.Output).Text);
        Assert.Null(_candle.Props);
    }

    [Fact]
    public async Task ACanceledTarget_ChangesNothing()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Trammel, new Point3D(1, 1, 0));

        var context = await RunAsync("lock");

        Assert.Equal("Target canceled.", Assert.Single(context.Output).Text);
        Assert.False(_left.Props!.ContainsKey("locked"));
    }

    [Fact]
    public async Task FromTheConsole_IsRefused()
    {
        _fixture = await SessionFixture.CreateAsync();
        var context = new CommandContext("lock", "lock", [], CommandSourceType.Console, null);

        await new LockCommand(_targets, _items, _templates, _fixture.Loop).ExecuteAsync(context);

        Assert.Equal(CommandOutputLevel.Error, Assert.Single(context.Output).Level);
        Assert.Equal(0, _targets.Requests);
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        _targets.Result = TargetResult.ForObject(_left.Id);

        var context = await RunAsync("lock", TestLocalization.With((30066, "La porta ora è chiusa a chiave.")));

        Assert.Equal("La porta ora è chiusa a chiave.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(string command, ILocalizationService? localization = null)
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        var context = new CommandContext($".{command}", command, [], CommandSourceType.InGame, session);
        ICommandExecutor executor = command == "lock"
            ? new LockCommand(_targets, _items, _templates, _fixture.Loop, localization)
            : new UnlockCommand(_targets, _items, _templates, _fixture.Loop, localization);

        await executor.ExecuteAsync(context);

        return context;
    }

    private static ItemEntity Door(uint serial, int x)
    {
        var door = new ItemEntity
        {
            Id = new Serial(serial), TemplateId = "decoration_door", ItemId = 0x0675, Amount = 1,
            Props = new() { ["facing"] = "west_cw" }
        };
        door.PlaceOnGround(MapType.Trammel, new Point3D(x, 1600, 0));

        return door;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
