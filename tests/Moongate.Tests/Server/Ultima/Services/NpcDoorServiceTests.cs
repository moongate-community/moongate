using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Ultima.Data.Bodies;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class NpcDoorServiceTests
{
    private const int Door = 0x0675;
    private const int Crate = 0x0E3D;

    private readonly SectorService _sectors = TestSectors.Create();
    private readonly ItemService _items;
    private readonly RecordingItemScriptService _scripts = new();
    private readonly NpcDoorService _doors;
    private readonly MobileEntity _guard = new()
    {
        Id = new Serial(0x100), Name = "a guard", TemplateId = "guard", Body = 0x190, Map = MapType.Trammel,
        Location = new Point3D(1600, 1600, 0)
    };

    public NpcDoorServiceTests()
    {
        _items = TestItems.Create(_sectors);
        _scripts.Scripted.Add("decoration_door");
        _scripts.Scripted.Add("plain_door");
        _doors = new(
            _sectors,
            new FakeTileDataService()
                .Item(Door, TileFlagType.Impassable | TileFlagType.Door, 20)
                .Item(Crate, TileFlagType.Impassable, 3),
            _scripts,
            new ItemTemplateService(
                new StubDataLoaderService().With(
                    new ItemTemplate { Id = "decoration_door", ItemId = new Serial(Door), ScriptId = "door" },
                    new ItemTemplate { Id = "plain_door", ItemId = new Serial(Door) }
                )
            ),
            new MobileTemplateService(
                new StubDataLoaderService().With(
                    new MobileTemplate { Id = "guard" },
                    new MobileTemplate { Id = "cat" },
                    new MobileTemplate { Id = "clever_cat", OpensDoors = true },
                    new MobileTemplate { Id = "ogre", OpensDoors = false }
                )
            ),
            new StubDataLoaderService().With(
                new BodyContent { Body = new(0x190), Type = BodyType.Human },
                new BodyContent { Body = new(0x01), Type = BodyType.Monster },
                new BodyContent { Body = new(0xC9), Type = BodyType.Animal },
                new BodyContent { Body = new(0x96), Type = BodyType.Sea }
            )
        );
    }

    [Theory]
    [InlineData("guard", 0x190, true)]
    [InlineData("guard", 0x01, true)]
    [InlineData("cat", 0xC9, false)]
    [InlineData("cat", 0x96, false)]
    [InlineData("clever_cat", 0xC9, true)]
    [InlineData("ogre", 0x01, false)]
    public void OpensDoors_IsForHumansAndMonsters_NotAnimalsOrSeaCreatures_UnlessTheTemplateSaysSo(
        string template,
        int body,
        bool opens
    )
    {
        _guard.TemplateId = template;
        _guard.Body = body;

        Assert.Equal(opens, _doors.OpensDoors(_guard));
    }

    [Fact]
    public void OpensDoors_IsFalseForAPlayer()
    {
        _guard.AccountId = new Serial(1);

        Assert.False(_doors.OpensDoors(_guard));
    }

    [Fact]
    public void TryOpen_AClosedDoorAhead_AsksItsScriptToOpenForTheNpc()
    {
        Ground(0x40000010, Door, 1601, 1600, 0);

        Assert.True(_doors.TryOpen(_guard, DirectionType.East));

        Assert.Equal("0x40000010 on_npc_use 256", Assert.Single(_scripts.Queued));
    }

    [Fact]
    public void TryOpen_ALockedDoor_AnOpenLeaf_ACrate_ADoorOnAnotherFloorOrNoScript_OpensNothing()
    {
        Ground(0x40000010, Door, 1601, 1600, 0).SetProp("locked", true);
        Assert.False(_doors.TryOpen(_guard, DirectionType.East));

        Ground(0x40000011, Door, 1599, 1600, 0).SetProp("door.open", true);
        Assert.False(_doors.TryOpen(_guard, DirectionType.West));

        Ground(0x40000012, Crate, 1600, 1599, 0);
        Assert.False(_doors.TryOpen(_guard, DirectionType.North));

        Ground(0x40000013, Door, 1600, 1601, 30);
        Assert.False(_doors.TryOpen(_guard, DirectionType.South));

        Ground(0x40000014, Door, 1601, 1601, 0, "plain_door");
        Assert.False(_doors.TryOpen(_guard, DirectionType.SouthEast));

        Assert.Empty(_scripts.Queued);
    }

    // A step along a diagonal is refused by a door on either cell beside it, as by one ahead.
    [Theory]
    [InlineData(1601, 1600)]
    [InlineData(1600, 1601)]
    [InlineData(1601, 1601)]
    public void TryOpen_ADoorBesideADiagonalStep_IsOpenedToo(int x, int y)
    {
        Ground(0x40000010, Door, x, y, 0);

        Assert.True(_doors.TryOpen(_guard, DirectionType.SouthEast));
        Assert.Equal("0x40000010 on_npc_use 256", Assert.Single(_scripts.Queued));
    }

    [Fact]
    public void TryOpen_ADoorBesideAStraightStep_IsLeftAlone()
    {
        Ground(0x40000010, Door, 1601, 1601, 0);

        Assert.False(_doors.TryOpen(_guard, DirectionType.East));
    }

    [Fact]
    public void TryOpen_AtADoorThatStaysShut_AsksThreeTimes_ThenRefuses_UntilTheNpcMoved()
    {
        Ground(0x40000010, Door, 1601, 1600, 0);

        var answers = Enumerable.Range(0, 5).Select(_ => _doors.TryOpen(_guard, DirectionType.East)).ToArray();

        // A caller that gives up on a refused step still does: the door is not asked for ever.
        Assert.Equal([true, true, true, false, false], answers);
        Assert.Equal(3, _scripts.Queued.Count);

        _doors.Moved(_guard);

        Assert.True(_doors.TryOpen(_guard, DirectionType.East));
    }

    [Fact]
    public void TryOpen_ForAnNpcThatDoesNotOpenDoors_OpensNothing()
    {
        Ground(0x40000010, Door, 1601, 1600, 0);
        _guard.TemplateId = "cat";
        _guard.Body = 0xC9;

        Assert.False(_doors.TryOpen(_guard, DirectionType.East));
        Assert.Empty(_scripts.Queued);
    }

    private ItemEntity Ground(uint serial, int graphic, int x, int y, int z, string template = "decoration_door")
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = template, ItemId = graphic, Amount = 1 };
        _items.Add([item]);
        _items.PlaceOnGround(item, MapType.Trammel, new Point3D(x, y, z));

        return item;
    }
}
