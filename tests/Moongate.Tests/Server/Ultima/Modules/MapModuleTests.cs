using Lua;
using Lua.Standard;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.MapItems;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.MapItems;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MapModuleTests : IAsyncLifetime
{
    private const long Map = 0x40000002;

    private readonly RecordingMapDisplayService _maps = new();
    private readonly StubItemHandlingService _handling = new();
    private readonly ItemService _items = TestItems.Create();

    private readonly ItemTemplateService _templates = new(
        new StubDataLoaderService().With<ItemTemplate>(new ItemTemplate { Id = "map", ItemId = new Serial(0x14EC) })
    );

    private readonly ItemEntity _map = new() { Id = new Serial((uint)Map), TemplateId = "map", ItemId = 0x14EC, Amount = 1 };

    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
        _items.Add([_map]);
    }

    [Fact]
    public void Display_ShowsTheMapToThePlayer()
    {
        Assert.True(Run($"return map.display(2, {Map})")[0].Read<bool>());
        Assert.Same(_map, Assert.Single(_maps.Displayed));

        Assert.False(Run($"return map.display(999, {Map})")[0].Read<bool>());
        Assert.False(Run("return map.display(2, 0x40009999)")[0].Read<bool>());
    }

    [Fact]
    public void SetBounds_ThenBounds_GiveTheArea()
    {
        Assert.True(Run($"return map.set_bounds({Map}, 100, 200, 300, 400, 200, 200, 1)")[0].Read<bool>());

        var area = Run($"local b = map.bounds({Map}) return b.x1, b.y1, b.x2, b.y2, b.width, b.height, b.facet");
        Assert.Equal([100, 200, 300, 400, 200, 200, 1], area.Select(value => (int)value.Read<double>()));
    }

    [Theory]
    [InlineData("300, 200, 100, 400, 200, 200, 0")]
    [InlineData("100, 200, 300, 400, 200, 200, 6")]
    [InlineData("100, 200, 300, 400, 0, 200, 0")]
    public void SetBounds_OfAnUpsideDownArea_AnUnknownFacet_OrNoDrawing_IsFalse(string arguments)
    {
        Assert.False(Run($"return map.set_bounds({Map}, {arguments})")[0].Read<bool>());
        Assert.True(Run($"return map.bounds({Map}) == nil")[0].Read<bool>());
    }

    [Fact]
    public void Pins_GoAndComeBack_AndMoreThanFiftyAreRefused()
    {
        Assert.True(Run($"return map.set_pins({Map}, {{ {{ x = 10, y = 20 }}, {{ x = 30, y = 40 }} }})")[0].Read<bool>());

        var pins = Run($"local p = map.pins({Map}) return #p, p[1].x, p[1].y, p[2].x, p[2].y");
        Assert.Equal([2, 10, 20, 30, 40], pins.Select(value => (int)value.Read<double>()));

        Assert.False(
            Run($"local p = {{}} for i = 1, 51 do p[i] = {{ x = i, y = i }} end return map.set_pins({Map}, p)")[0].Read<bool>()
        );
        Assert.Equal(2, MapItemProps.GetPins(_map).Count);
    }

    [Fact]
    public void AddWorldPin_PutsATileOfTheWorldOnTheDrawing()
    {
        Run($"map.set_bounds({Map}, 0, 0, 5120, 4096, 200, 200, 0)");

        Assert.True(Run($"return map.add_world_pin({Map}, 2560, 1024)")[0].Read<bool>());
        Assert.False(Run($"return map.add_world_pin({Map}, 9000, 1024)")[0].Read<bool>());

        Assert.Equal([(100, 50)], MapItemProps.GetPins(_map));
    }

    [Fact]
    public void EditableAndProtected_AreSet()
    {
        Assert.True(Run($"return map.set_editable({Map}, true)")[0].Read<bool>());
        Assert.True(Run($"return map.set_protected({Map}, true)")[0].Read<bool>());

        Assert.True(MapItemProps.IsEditable(_map));
        Assert.True(MapItemProps.IsProtected(_map));
    }

    [Fact]
    public void AMapOnACursor_IsNotChanged()
    {
        _handling.Held.Add(_map);

        Assert.False(Run($"return map.set_bounds({Map}, 100, 200, 300, 400, 200, 200, 1)")[0].Read<bool>());
        Assert.False(Run($"return map.set_editable({Map}, true)")[0].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(
            state,
            new MapModule(_items, _templates, _fixture.Sessions, _maps, _handling)
        );

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
