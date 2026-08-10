using Moongate.Core.Interfaces;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.UO.Data.Items;
using Moongate.UO.Data.World;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Interfaces.Timing;

namespace Moongate.Tests.Server.World;

/// <summary>
/// Turning found doorways into doors.
/// <para>
/// The scan itself is tested where it lives; what is held to account here is everything around it —
/// that the doorway becomes a door of the right kind, that running it twice does not fill a doorway
/// twice, and that the 21.4 million tiles it reads are read off the game loop while the items it
/// creates are written on it.
/// </para>
/// </summary>
public class DoorGenerationServiceTests
{
    private const int WestFrame = 0x0007;
    private const int EastFrame = 0x000A;

    [Fact]
    public async Task Generate_ADoorwayInAStoneWall_BecomesAMetalDoor()
    {
        var world = new Harness();

        var result = await world.Service.GenerateAsync();

        Assert.Equal(1, result.Placed);

        var door = Assert.Single(world.Spatial.GetItemsInRange(1, new(11, 10, 0), 0));

        Assert.Equal("metal_door", door.TemplateId);
        Assert.Equal("items.door", door.ScriptId);

        // metal_door's base is 0x675 and the doorway faces WestCW, which is facing 0.
        Assert.Equal(0x675, door.ItemId);
    }

    [Fact]
    public async Task Generate_ADoorwayInAWoodenWall_BecomesAWoodenDoor()
    {
        var world = new Harness { WallName = "wooden wall" };

        await world.Service.GenerateAsync();

        Assert.Equal(
            "strong_wood_door",
            Assert.Single(world.Spatial.GetItemsInRange(1, new(11, 10, 0), 0)).TemplateId
        );
    }

    // The graphic is the facing: a door hung the other way must not come out of the same block.
    [Fact]
    public async Task Generate_ADoorwayFacingTheOtherWay_TakesTheOtherGraphic()
    {
        var world = new Harness { Doorway = Doorways.NorthSouth };

        await world.Service.GenerateAsync();

        var door = Assert.Single(world.Spatial.GetItemsInRange(1, new(10, 11, 0), 0));

        // SouthCW is facing 4, so base + 8.
        Assert.Equal(0x675 + 8, door.ItemId);
    }

    // The one that matters: this runs from a command, and a doorway holds one door.
    [Fact]
    public async Task Generate_RunTwice_DoesNotFillADoorwayTwice()
    {
        var world = new Harness();

        await world.Service.GenerateAsync();
        var second = await world.Service.GenerateAsync();

        Assert.Equal(0, second.Placed);
        Assert.Equal(1, second.Skipped);
        Assert.Single(world.Spatial.GetItemsInRange(1, new(11, 10, 0), 0));
    }

    // 21.4 million tile lookups cannot happen on the loop, and the items cannot happen off it.
    [Fact]
    public async Task Generate_ReadsTheMapOffTheLoopAndWritesTheItemsOnIt()
    {
        var world = new Harness();

        await world.Service.GenerateAsync();

        Assert.False(world.ReadTheMapOnTheLoop, "the scan read the map on the game loop");
        Assert.True(world.Loop.PostCount > 0, "the placement never reached the game loop");
    }

    [Fact]
    public async Task Generate_AWorldWithNoDoorways_PlacesNothing()
    {
        var world = new Harness { Doorway = Doorways.None };

        Assert.Equal(0, (await world.Service.GenerateAsync()).Placed);
    }

    private enum Doorways
    {
        None,
        WestEast,
        NorthSouth
    }

    /// <summary>
    /// A game loop that says whether it is running work right now, so a test can tell what happened on
    /// it from what happened beside it. It runs the work inline, which is enough: the scan either read
    /// the map before anything was posted, or it read it inside posted work.
    /// </summary>
    private sealed class DeferredLoop : IGameLoopContext
    {
        public DeferredLoop(bool inline)
        {
            Inline = inline;
        }

        public bool Inline { get; }

        /// <summary>True while this loop is executing work handed to it.</summary>
        public bool Running { get; private set; }

        public int PostCount { get; private set; }

        public IMainThreadDispatcher Dispatcher => throw new NotSupportedException();

        public ITimerService Timers => throw new NotSupportedException();

        public bool Cancel(string timerId)
            => false;

        public Task<T> InvokeAsync<T>(Func<T> work, TimeSpan? timeout = null)
        {
            PostCount++;
            Running = true;

            try
            {
                return Task.FromResult(work());
            }
            finally
            {
                Running = false;
            }
        }

        public void Post(Action action)
        {
            PostCount++;
            Running = true;

            try
            {
                action();
            }
            finally
            {
                Running = false;
            }
        }

        public string Schedule(string name, TimeSpan delay, Action callback)
            => throw new NotSupportedException();

        public string ScheduleRepeating(string name, TimeSpan interval, Action callback, TimeSpan? delay = null)
            => throw new NotSupportedException();
    }

    /// <summary>A world with one doorway in it, and a record of which thread read the map.</summary>
    private sealed class Harness
    {
        private readonly FakePersistenceService _persistence = new();

        public Harness()
        {
            Spatial = new(_persistence, new StubLoopAffinity(), new StubEventBus());

            var templates = new ItemTemplateService();

            foreach (var (id, itemId) in new[] { ("metal_door", 0x675), ("strong_wood_door", 0x6E5), ("dark_wood_door", 0x6A5) })
            {
                templates.Register(
                    new ItemTemplate
                    {
                        Id = id, Name = "", Category = "Structure", ItemId = itemId, ScriptId = "items.door"
                    }
                );
            }

            Loop = new(inline: false);

            // A hand-sized region: the real ones are 21.4 million tiles between them.
            Service = new(
                new ItemFactoryService(templates, new(1)),
                new ItemService(_persistence, spatial: Spatial),
                Spatial,
                templates,
                Loop,
                StaticsAt,
                NameOf,
                new Dictionary<int, IReadOnlyList<DoorScanRegion>> { [1] = [new(0, 0, 20, 20)] }
            );
        }

        public SpatialIndexService Spatial { get; }

        public DeferredLoop Loop { get; }

        public DoorGenerationService Service { get; }

        public string WallName { get; init; } = "stone wall";

        public Doorways Doorway { get; init; } = Doorways.WestEast;

        /// <summary>True when the map was read while the loop was running work.</summary>
        public bool ReadTheMapOnTheLoop { get; private set; }

        private string NameOf(int itemId)
            => WallName;

        private IReadOnlyList<(int Id, int Z)> StaticsAt(int mapId, int x, int y)
        {
            if (Loop.Running)
            {
                ReadTheMapOnTheLoop = true;
            }

            return Doorway switch
            {
                Doorways.WestEast when (x, y) == (10, 10)  => [(WestFrame, 0)],
                Doorways.WestEast when (x, y) == (12, 10)  => [(EastFrame, 0)],
                Doorways.NorthSouth when (x, y) == (10, 10) => [(0x0006, 0)],
                Doorways.NorthSouth when (x, y) == (10, 12) => [(0x0008, 0)],
                _                                          => []
            };
        }
    }
}
