using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class WorldModuleTests
{
    private readonly SectorService _sectors = TestSectors.Create();

    public WorldModuleTests()
    {
        _sectors.Add(new MobileEntity { Id = new Serial(0x100), Name = "orc", Map = MapType.Trammel, Location = new Point3D(1600, 1600, 0) });
        _sectors.Add(
            new MobileEntity
            {
                Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca, Location = new Point3D(1400, 1600, 0)
            }
        );
    }

    [Theory,
     InlineData("return world.is_occupied(MapType.Trammel, 1600, 1600)", true),
     InlineData("return world.is_occupied('Felucca', 1400, 1600)", true),
     InlineData("return world.is_occupied(MapType.Trammel, 1601, 1600)", false),
     InlineData("return world.is_occupied(MapType.Felucca, 1600, 1600)", false)]
    public void IsOccupied_TellsWhetherAMobileStandsOnTheTile(string chunk, bool expected)
    {
        Assert.Equal(expected, Run(chunk)[0].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(state, new WorldModule(_sectors));
        binder.BindEnum(state, typeof(MapType));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
