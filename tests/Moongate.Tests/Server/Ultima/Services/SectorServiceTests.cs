using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Npcs;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SectorServiceTests
{

    [Fact]
    public void GetMobilesInRange_FindsAnAddedMobile()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);

        sectors.Add(aria);

        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Theory]
    [InlineData(18, 0, true)]
    [InlineData(0, 18, true)]
    [InlineData(-18, -18, true)]
    [InlineData(19, 0, false)]
    [InlineData(0, -19, false)]
    public void GetMobilesInRange_UsesTheSquareRangeAcrossSectors(int dx, int dy, bool expected)
    {
        var sectors = TestSectors.Create();
        // 1600 is a sector edge (100 * 16): the other mobile sits in another sector for every case.
        var other = Mobile(3, 1600 + dx, 1600 + dy);
        sectors.Add(other);

        var found = sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1600, 1600, 0), 18);

        Assert.Equal(expected, found.Contains(other));
    }

    [Fact]
    public void Move_AcrossASectorEdge_KeepsTheMobileFindableAndOnlyOnce()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1599, 1600);
        sectors.Add(aria);

        aria.Location = new Point3D(1600, 1600, 0);
        sectors.Move(aria);

        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1600, 1600, 0), 18));
        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1580, 1600, 0), 20));
    }

    [Fact]
    public void Move_FarAway_LeavesTheOldArea()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);
        sectors.Add(aria);

        aria.Location = new Point3D(3000, 3000, 0);
        sectors.Move(aria);

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(3000, 3000, 0), 18));
    }

    [Fact]
    public void Remove_ForgetsTheMobile()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);
        sectors.Add(aria);

        sectors.Remove(aria);

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void GetMobilesInRange_DoesNotMixMaps()
    {
        var sectors = TestSectors.Create();
        sectors.Add(Mobile(2, 1496, 1628, MapType.Felucca));

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void Add_OutsideTheMap_IsIgnored()
    {
        var sectors = TestSectors.Create();
        var lost = Mobile(2, 9000, 100);

        sectors.Add(lost);
        sectors.Move(lost);
        sectors.Remove(lost);

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(7160, 100, 0), 18));
    }

    [Fact]
    public void Add_OnAMapWithoutContent_IsIgnored()
    {
        var sectors = new SectorService(new StubDataLoaderService(), new WorldConfig(), new RecordingNpcTickService());

        sectors.Add(Mobile(2, 1496, 1628));

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void GetMobilesInRange_NearTheMapCorner_DoesNotThrow()
    {
        var sectors = TestSectors.Create();
        var corner = Mobile(2, 0, 0);
        sectors.Add(corner);

        Assert.Equal([corner], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(0, 0, 0), 18));
        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(7167, 4095, 0), 18));
    }

    [Fact]
    public void GetItemsAt_GivesTheItemsOfThatCellOnly_AndFollowsThemWhenTheyMove()
    {
        var sectors = TestSectors.Create();
        var door = new ItemEntity { Id = new Serial(0x40000010), TemplateId = "door", ItemId = 0x0675, Amount = 1 };
        var sign = new ItemEntity { Id = new Serial(0x40000011), TemplateId = "sign", ItemId = 0x0BD2, Amount = 1 };
        door.PlaceOnGround(MapType.Trammel, new Point3D(1496, 1628, 0));
        sign.PlaceOnGround(MapType.Trammel, new Point3D(1496, 1628, 20));
        sectors.AddItem(door);
        sectors.AddItem(sign);

        Assert.Equal([door, sign], sectors.GetItemsAt(MapType.Trammel, 1496, 1628));
        Assert.Empty(sectors.GetItemsAt(MapType.Trammel, 1497, 1628));
        Assert.Empty(sectors.GetItemsAt(MapType.Felucca, 1496, 1628));

        // The door swings open onto the next cell.
        door.PlaceOnGround(MapType.Trammel, new Point3D(1497, 1627, 0));
        sectors.AddItem(door);

        Assert.Equal([sign], sectors.GetItemsAt(MapType.Trammel, 1496, 1628));
        Assert.Equal([door], sectors.GetItemsAt(MapType.Trammel, 1497, 1627));

        sectors.RemoveItem(door);
        sectors.RemoveItem(sign);

        Assert.Empty(sectors.GetItemsAt(MapType.Trammel, 1497, 1627));
        Assert.Empty(sectors.GetItemsAt(MapType.Trammel, 1496, 1628));
    }

    [Fact]
    public void GetItemsInRange_FindsAGroundItem()
    {
        var sectors = TestSectors.Create();
        var gold = GroundItem(0x40000001, 1496, 1628);

        sectors.AddItem(gold);

        Assert.Equal([gold], sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496 + 19, 1628, 0), 18));
        Assert.Empty(sectors.GetItemsInRange(MapType.Felucca, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void RemoveItem_ForgetsIt()
    {
        var sectors = TestSectors.Create();
        var gold = GroundItem(0x40000001, 1496, 1628);
        sectors.AddItem(gold);

        sectors.RemoveItem(gold);
        sectors.RemoveItem(gold);

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void AddItem_NotOnTheGround_IsIgnored()
    {
        var sectors = TestSectors.Create();
        var coins = new ItemEntity { Id = new(0x40000002), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        coins.PutInContainer(new Serial(0x40000009), new Point2D(1, 1));

        sectors.AddItem(coins);

        Assert.Empty(sectors.GetItemsInRange(MapType.Trammel, new Point3D(0, 0, 0), 7000));
    }

    [Fact]
    public void GetMobilesInRange_DoesNotReturnItems()
    {
        var sectors = TestSectors.Create();
        sectors.AddItem(GroundItem(0x40000001, 1496, 1628));

        Assert.Empty(sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1496, 1628, 0), 18));
    }

    [Fact]
    public void Query_SplitsPlayersNpcsAndItemsInRange()
    {
        var sectors = TestSectors.Create();
        var aria = Mobile(2, 1496, 1628);
        aria.AccountId = new Serial(0x42);
        var orc = Mobile(0x100, 1500, 1628);
        var far = Mobile(0x101, 1600, 1628);
        var gold = GroundItem(0x40000001, 1498, 1628);
        sectors.Add(aria);
        sectors.Add(orc);
        sectors.Add(far);
        sectors.AddItem(gold);

        var result = sectors.Query(MapType.Trammel, new Point3D(1496, 1628, 0));

        Assert.Equal([aria], result.Players);
        Assert.Equal([orc], result.Npcs);
        Assert.Equal([gold], result.Items);
    }

    [Fact]
    public void Query_WithoutARange_UsesTheConfiguredViewRange()
    {
        var sectors = TestSectors.Create(new WorldConfig { ViewRange = 5 });
        var orc = Mobile(0x100, 1502, 1628);
        sectors.Add(orc);

        Assert.Empty(sectors.Query(MapType.Trammel, new Point3D(1496, 1628, 0)).Npcs);
        Assert.Equal([orc], sectors.Query(MapType.Trammel, new Point3D(1496, 1628, 0), 6).Npcs);
    }

    [Theory]
    [InlineData(1600, 1600, true)]
    [InlineData(1600 + 2 * 16, 1600, true)]
    [InlineData(1600 - 2 * 16, 1600 + 2 * 16, true)]
    [InlineData(1600 + 3 * 16, 1600, false)]
    [InlineData(1600, 1600 - 3 * 16, false)]
    public void IsActive_WithinTwoSectorsOfAPlayer(int x, int y, bool expected)
    {
        var sectors = TestSectors.Create();

        sectors.Add(Player(2, 1600, 1600));

        Assert.Equal(expected, sectors.IsActive(MapType.Trammel, new Point3D(x, y, 0)));
    }

    [Fact]
    public void IsActive_NobodyOrOnlyNpcs_IsFalse()
    {
        var sectors = TestSectors.Create();

        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600, 1600, 0)));

        sectors.Add(Mobile(0x100, 1600, 1600));

        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600, 1600, 0)));
    }

    [Fact]
    public void Remove_ThePlayer_PutsTheSectorsToSleep()
    {
        var sectors = TestSectors.Create();
        var aria = Player(2, 1600, 1600);
        sectors.Add(aria);

        sectors.Remove(aria);

        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600, 1600, 0)));
    }

    [Fact]
    public void Move_ThePlayer_MovesTheActiveBlock()
    {
        var sectors = TestSectors.Create();
        var aria = Player(2, 1600, 1600);
        sectors.Add(aria);

        aria.Location = new Point3D(1600 + 16, 1600, 0);
        sectors.Move(aria);

        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600 - 2 * 16, 1600, 0)));
        Assert.True(sectors.IsActive(MapType.Trammel, new Point3D(1600 + 3 * 16, 1600, 0)));
    }

    [Fact]
    public void Remove_OneOfTwoPlayers_KeepsTheSharedSectorsAwake()
    {
        var sectors = TestSectors.Create();
        var aria = Player(2, 1600, 1600);
        sectors.Add(aria);
        sectors.Add(Player(3, 1600 + 4 * 16, 1600));

        sectors.Remove(aria);

        Assert.True(sectors.IsActive(MapType.Trammel, new Point3D(1600 + 2 * 16, 1600, 0)));
        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600 + 16, 1600, 0)));
    }

    [Fact]
    public void IsActive_DoesNotMixMaps()
    {
        var sectors = TestSectors.Create();

        sectors.Add(Player(2, 1600, 1600));

        Assert.False(sectors.IsActive(MapType.Felucca, new Point3D(1600, 1600, 0)));
    }

    [Fact]
    public void Add_APlayerAtTheMapCorner_ActivatesOnlyInsideTheMap()
    {
        var sectors = TestSectors.Create();

        sectors.Add(Player(2, 0, 0));

        Assert.True(sectors.IsActive(MapType.Trammel, new Point3D(0, 0, 0)));
        Assert.True(sectors.IsActive(MapType.Trammel, new Point3D(2 * 16, 2 * 16, 0)));
        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(-1, 0, 0)));
    }

    [Fact]
    public void Add_APlayer_WakesTheNpcsInTheFiveByFiveAndNoFurther()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        var near = Mobile(0x100, 1600 + 2 * 16, 1600);
        var far = Mobile(0x101, 1600 + 3 * 16, 1600);
        sectors.Add(near);
        sectors.Add(far);

        sectors.Add(Player(2, 1600, 1600));

        Assert.True(ticks.IsAwake(near.Id));
        Assert.False(ticks.IsAwake(far.Id));
    }

    [Fact]
    public void Remove_TheLastPlayer_PutsTheNpcsToSleep()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        var npc = Mobile(0x100, 1600, 1600);
        var aria = Player(2, 1600, 1600);
        sectors.Add(npc);
        sectors.Add(aria);

        sectors.Remove(aria);

        Assert.False(ticks.IsAwake(npc.Id));
    }

    [Fact]
    public void Remove_OneOfTwoPlayers_KeepsTheSharedNpcsAwake()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        var shared = Mobile(0x100, 1600 + 2 * 16, 1600);
        var aria = Player(2, 1600, 1600);
        sectors.Add(shared);
        sectors.Add(aria);
        sectors.Add(Player(3, 1600 + 4 * 16, 1600));

        sectors.Remove(aria);

        Assert.True(ticks.IsAwake(shared.Id));
        Assert.Equal(1, ticks.Wakes);
    }

    [Fact]
    public void Move_APlayerAcrossASectorEdge_DoesNotRestartTheNpcsBothPositionsCover()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        var npc = Mobile(0x100, 1600, 1600);
        var aria = Player(2, 1599, 1600);
        sectors.Add(npc);
        sectors.Add(aria);

        aria.Location = new Point3D(1600, 1600, 0);
        sectors.Move(aria);

        Assert.True(ticks.IsAwake(npc.Id));
        Assert.Equal(1, ticks.Wakes);
        Assert.Equal(0, ticks.Sleeps);
    }

    [Fact]
    public void Move_APlayerAway_PutsTheNpcsLeftBehindToSleep()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        var npc = Mobile(0x100, 1600, 1600);
        var aria = Player(2, 1600, 1600);
        sectors.Add(npc);
        sectors.Add(aria);

        aria.Location = new Point3D(3000, 3000, 0);
        sectors.Move(aria);

        Assert.False(ticks.IsAwake(npc.Id));
    }

    [Fact]
    public void Add_AnNpcNextToAPlayer_WakesIt_FarAway_DoesNot()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        sectors.Add(Player(2, 1600, 1600));
        var near = Mobile(0x100, 1610, 1600);
        var far = Mobile(0x101, 3000, 3000);

        sectors.Add(near);
        sectors.Add(far);

        Assert.True(ticks.IsAwake(near.Id));
        Assert.False(ticks.IsAwake(far.Id));
    }

    [Fact]
    public void Move_AnNpcOutOfTheActiveArea_SleepsAndBackIn_Wakes()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        sectors.Add(Player(2, 1600, 1600));
        var npc = Mobile(0x100, 1600 + 2 * 16, 1600);
        sectors.Add(npc);

        npc.Location = new Point3D(1600 + 3 * 16, 1600, 0);
        sectors.Move(npc);
        Assert.False(ticks.IsAwake(npc.Id));

        npc.Location = new Point3D(1600 + 2 * 16, 1600, 0);
        sectors.Move(npc);
        Assert.True(ticks.IsAwake(npc.Id));
    }

    [Fact]
    public void Move_AnNpcAcrossASectorEdgeInsideTheActiveArea_KeepsItsTimer()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        sectors.Add(Player(2, 1600, 1600));
        var npc = Mobile(0x100, 1599, 1600);
        sectors.Add(npc);

        npc.Location = new Point3D(1600, 1600, 0);
        sectors.Move(npc);

        Assert.True(ticks.IsAwake(npc.Id));
        Assert.Equal(1, ticks.Wakes);
        Assert.Equal(0, ticks.Sleeps);
    }

    [Fact]
    public void Remove_AnAwakeNpc_PutsItToSleep()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        sectors.Add(Player(2, 1600, 1600));
        var npc = Mobile(0x100, 1600, 1600);
        sectors.Add(npc);

        sectors.Remove(npc);

        Assert.False(ticks.IsAwake(npc.Id));
    }

    [Fact]
    public void Move_AnAwakeNpcOffTheMap_PutsItToSleep()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        sectors.Add(Player(2, 1600, 1600));
        var npc = Mobile(0x100, 1600, 1600);
        sectors.Add(npc);

        npc.Location = new Point3D(9000, 1600, 0);
        sectors.Move(npc);

        Assert.False(ticks.IsAwake(npc.Id));
    }

    [Fact]
    public void Move_APlayerToAnotherMap_PutsTheOldNpcsToSleepAndWakesTheNewOnes()
    {
        var ticks = new RecordingNpcTickService();
        var sectors = TestSectors.Create(ticks: ticks);
        var trammelOrc = Mobile(0x100, 1600, 1600);
        var feluccaOrc = Mobile(0x101, 1600, 1600, MapType.Felucca);
        var aria = Player(2, 1600, 1600);
        sectors.Add(trammelOrc);
        sectors.Add(feluccaOrc);
        sectors.Add(aria);

        aria.Map = MapType.Felucca;
        sectors.Move(aria);

        Assert.False(ticks.IsAwake(trammelOrc.Id));
        Assert.True(ticks.IsAwake(feluccaOrc.Id));
        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600, 1600, 0)));
    }

    [Fact]
    public void Move_APlayerStepByStep_LeavesNoActiveSectorBehind()
    {
        var sectors = TestSectors.Create();
        var aria = Player(2, 1600, 1600);
        sectors.Add(aria);

        for (var x = 1601; x <= 1600 + 6 * 16; x++)
        {
            aria.Location = new Point3D(x, 1600, 0);
            sectors.Move(aria);
        }

        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600, 1600, 0)));
        Assert.False(sectors.IsActive(MapType.Trammel, new Point3D(1600 + 3 * 16, 1600, 0)));
        Assert.True(sectors.IsActive(MapType.Trammel, new Point3D(1600 + 4 * 16, 1600, 0)));
        Assert.Equal([aria], sectors.GetMobilesInRange(MapType.Trammel, new Point3D(1600 + 6 * 16, 1600, 0), 18));
    }

    private static ItemEntity GroundItem(uint serial, int x, int y)
    {
        var item = new ItemEntity { Id = new Serial(serial), TemplateId = "gold", ItemId = 0x0EED, Amount = 1 };
        item.PlaceOnGround(MapType.Trammel, new Point3D(x, y, 0));

        return item;
    }

    private static MobileEntity Player(uint serial, int x, int y)
    {
        var player = Mobile(serial, x, y);
        player.AccountId = new Serial(0x42);

        return player;
    }

    private static MobileEntity Mobile(uint serial, int x, int y, MapType map = MapType.Trammel)
    {
        return new() { Id = new Serial(serial), Name = $"M{serial}", Map = map, Location = new Point3D(x, y, 0) };
    }
}
