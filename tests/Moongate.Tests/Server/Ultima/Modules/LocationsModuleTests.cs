using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class LocationsModuleTests
{
    private readonly List<NamedLocation> _places =
    [
        new() { Map = MapType.Felucca, Category = "Towns/Britain", Name = "Bank", Location = new Point3D(1434, 1699, 2) },
        new() { Map = MapType.Felucca, Category = "Towns", Name = "Cove", Location = new Point3D(2275, 1210, 0) },
        new() { Map = MapType.Trammel, Category = "Towns", Name = "Cove", Location = new Point3D(2276, 1211, -5) }
    ];

    [Fact]
    public void Node_TheTop_ListsTheMapsWithTheirPaths()
    {
        var result = Run(
            """
            local top = locations.node("")
            return top.path, top.name, #top.categories, top.categories[1].name, top.categories[1].path,
                top.categories[2].name, #top.locations
            """
        );

        Assert.Equal(["", ""], result[..2].Select(value => value.Read<string>()));
        Assert.Equal(2, result[2].Read<int>());
        Assert.Equal(["Felucca", "Felucca", "Trammel"], result[3..6].Select(value => value.Read<string>()));
        Assert.Equal(0, result[6].Read<int>());
    }

    [Fact]
    public void Node_ACategory_GivesItsCategoriesAndItsPlaces()
    {
        var result = Run(
            """
            local towns = locations.node("felucca/towns")
            local cove = towns.locations[1]
            return towns.path, towns.name, towns.categories[1].name, towns.categories[1].path, cove.name, cove.category,
                #towns.locations, cove.x, cove.y, cove.z, cove.map
            """
        );

        Assert.Equal(
            ["Felucca/Towns", "Towns", "Britain", "Felucca/Towns/Britain", "Cove", "Towns"],
            result[..6].Select(value => value.Read<string>())
        );
        Assert.Equal([1, 2275, 1210, 0, (int)MapType.Felucca], result[6..].Select(value => value.Read<int>()));
    }

    [Fact]
    public void Node_AnUnknownPath_IsNil()
    {
        Assert.Equal(LuaValueType.Nil, Run("""return locations.node("felucca/nowhere")""")[0].Type);
    }

    [Fact]
    public void Find_GivesThePlacesOfTheMapFirst()
    {
        var result = Run(
            """
            local found = locations.find("cove", 1)
            return #found, found[1].name, found[1].category, found[1].map, found[1].z
            """
        );

        Assert.Equal(1, result[0].Read<int>());
        Assert.Equal(["Cove", "Towns"], result[1..3].Select(value => value.Read<string>()));
        Assert.Equal([(int)MapType.Trammel, -5], result[3..].Select(value => value.Read<int>()));
    }

    [Fact]
    public void Find_NothingFound_IsAnEmptyTable()
    {
        Assert.Equal(0, Run("""return #locations.find("atlantis", 0)""")[0].Read<int>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var service = new LocationService(new StubDataLoaderService().With(_places.ToArray()), TestSectors.Create());
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new LocationsModule(service));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
