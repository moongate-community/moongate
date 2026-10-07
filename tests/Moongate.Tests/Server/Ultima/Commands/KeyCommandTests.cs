using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Localization;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Commands;

public sealed class KeyCommandTests : IAsyncDisposable
{
    private readonly StubTargetService _targets = new();
    private readonly StubPacketSendService _sender = new();
    private readonly ItemService _items = TestItems.Create();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With(
            new ItemTemplate { Id = "decoration_door", ItemId = new Serial(0x0675), ScriptId = "door" },
            new ItemTemplate { Id = "0x1010_iron_key", ItemId = new Serial(0x1010), Name = "iron key" }
        )
    );

    private readonly FakeItemFactoryService _factory;

    private readonly ItemEntity _backpack = new()
        { Id = new Serial(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };

    private readonly ItemEntity _door = new()
    {
        Id = new Serial(0x40000010), TemplateId = "decoration_door", ItemId = 0x0675, Amount = 1,
        Props = new() { ["key.value"] = 555L }
    };

    private SessionFixture? _fixture;

    public KeyCommandTests()
    {
        _factory = new(_templates, new FakeTileDataService());
        _backpack.Equip(new Serial(2), LayerType.Backpack);
        _door.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        _items.Add([_backpack, _door]);
    }

    [Fact]
    public async Task ReservedBackpack_RefusesBeforeCreatingTheKeyOrChangingTheDoor()
    {
        _door.RemoveProp("key.value");
        _targets.Result = TargetResult.ForObject(_door.Id);
        var reservations =
            new Moongate.Server.Ultima.Services.Items.InventoryReservationService(
                new Moongate.Tests.TestSupport.Scripting.StubGameLoop()
            );
        reservations.TryReserve(new(2), Task.CompletedTask);
        var guard = new Moongate.Server.Ultima.Services.Items.InventoryMutationGuard(
            new Lazy<Moongate.Server.Ultima.Interfaces.IItemService>(() => _items),
            reservations
        );
        await RunAsync(inventory: guard, reservations: reservations);
        Assert.Empty(_factory.Saved);
        Assert.False(_door.TryGetProp<long>("key.value", out _));
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task ADoor_PutsAKeyWithItsValueInTheBackpack_AndShowsIt()
    {
        _targets.Result = TargetResult.ForObject(_door.Id);

        var context = await RunAsync();

        var key = Assert.Single(_items.GetContents(_backpack.Id));
        Assert.Equal(("0x1010_iron_key", (object?)555L), (key.TemplateId, key.Props?.GetValueOrDefault("key.value")));
        Assert.True(key.Id.IsItem);
        Assert.Equal(key.Id, Assert.Single(_sender.Sent.OfType<ContainerItemUpdatePacket>()).Item.Serial);
        Assert.Equal("A key for the door is in your backpack.", Assert.Single(context.Output).Text);
    }

    [Fact]
    public async Task ADoorWithoutAKey_GetsOne_ForTheKeyToo()
    {
        _door.RemoveProp("key.value");
        _targets.Result = TargetResult.ForObject(_door.Id);

        await RunAsync();

        var value = Assert.IsType<long>(_door.Props!["key.value"]);
        Assert.Equal(value, Assert.Single(_items.GetContents(_backpack.Id)).Props!["key.value"]);
    }

    [Fact]
    public async Task SomethingThatIsNotADoor_IsRefused()
    {
        _targets.Result = TargetResult.ForObject(_backpack.Id);

        var context = await RunAsync();

        Assert.Equal("That is not a door.", Assert.Single(context.Output).Text);
        Assert.Empty(_items.GetContents(_backpack.Id));
    }

    [Fact]
    public async Task Texts_AreInTheServerLanguage()
    {
        _targets.Result = TargetResult.ForObject(_door.Id);

        var context = await RunAsync(TestLocalization.With((30071, "La chiave è nel tuo zaino.")));

        Assert.Equal("La chiave è nel tuo zaino.", Assert.Single(context.Output).Text);
    }

    private async Task<CommandContext> RunAsync(
        ILocalizationService? localization = null,
        Moongate.Server.Ultima.Interfaces.Items.IInventoryMutationGuard? inventory = null,
        Moongate.Server.Ultima.Interfaces.Items.IInventoryReservationService? reservations = null
    )
    {
        _fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(_fixture.Loop).GetOrCreate(_fixture.Client);
        await _fixture.ExecuteOnLoopAsync(() => session.Set(SessionKeys.CharacterId, new Serial(2)));
        var context = new CommandContext(".key", "key", [], CommandSourceType.InGame, session);

        await new KeyCommand(
                _targets,
                _items,
                _templates,
                _factory,
                _sender,
                TestTooltips.Create(_items, _mobiles),
                _fixture.Loop,
                localization,
                inventory,
                reservations
            )
            .ExecuteAsync(context);

        return context;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }
}
