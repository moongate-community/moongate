using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class MoveOverServiceTests
{
    private const int Pad = 0x1BC3;
    private const int Table = 0x0B34;

    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;
    private readonly RecordingItemScriptService _scripts = new();
    private readonly MoveOverService _moveOver;

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), AccountId = new Serial(0x42), Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0)
    };

    public MoveOverServiceTests()
    {
        _items = TestItems.Create(_sectors);
        _scripts.Scripted.Add("teleporter");
        _moveOver = new(_sectors, _scripts, new FakeTileDataService().Item(Table, TileFlagType.Surface, 6));
    }

    [Fact]
    public void SteppedOn_AScriptedItemOnTheCell_RunsItsOnMoveOverWithTheMobile()
    {
        Ground(0x40000010, 1600, 1600, 0);

        _moveOver.SteppedOn(_aria);

        Assert.Equal(["0x40000010 on_move_over 2"], _scripts.Calls);
    }

    [Fact]
    public void SteppedOn_ByAnNpc_RunsOnNpcMoveOverInstead()
    {
        Ground(0x40000010, 1600, 1600, 0);
        var orc = new MobileEntity
            { Id = new Serial(0x100), TemplateId = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) };

        _moveOver.SteppedOn(orc);

        Assert.Equal(["0x40000010 on_npc_move_over 256"], _scripts.Calls);
    }

    [Fact]
    public void SteppedOn_ItemsOnOtherCellsOrWithoutAScript_AreLeftAlone()
    {
        Ground(0x40000010, 1601, 1600, 0);
        Ground(0x40000011, 1600, 1600, 0, template: "gold");

        _moveOver.SteppedOn(_aria);

        Assert.Empty(_scripts.Calls);
    }

    [Theory]
    [InlineData(Pad, 0, true)]
    // Above the feet, within a mobile's height.
    [InlineData(Pad, 14, true)]
    [InlineData(Pad, 15, false)]
    // Below the feet: only an item tall enough to reach them.
    [InlineData(Pad, -1, false)]
    [InlineData(Table, -5, true)]
    [InlineData(Table, -6, false)]
    public void SteppedOn_AnItemAtAnotherHeight_CountsAsInModernUo(int graphic, int z, bool run)
    {
        Ground(0x40000010, 1600, 1600, z, graphic);

        _moveOver.SteppedOn(_aria);

        Assert.Equal(run ? 1 : 0, _scripts.Calls.Count);
    }

    [Fact]
    public void SteppedOn_OnceAScriptMovedTheMobileAway_TheOtherItemsOfTheCellAreNotRun()
    {
        Ground(0x40000010, 1600, 1600, 0);
        Ground(0x40000011, 1600, 1600, 0);
        var scripts = new MovingItemScriptService(_aria, new Point3D(5690, 569, 25));

        new MoveOverService(_sectors, scripts, new FakeTileDataService()).SteppedOn(_aria);

        Assert.Equal(1, scripts.Runs);
    }

    private void Ground(uint serial, int x, int y, int z, int graphic = Pad, string template = "teleporter")
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = template, ItemId = graphic, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, MapType.Trammel, new Point3D(x, y, z));
    }
}
