using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Ultima.Moongates;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MoongatesModuleTests
{
    private readonly StubPublicMoongateService _moongates = new();

    [Fact]
    public void Facets_GivesEveryMapWithItsDestinationsInOrder()
    {
        _moongates.Facets.Add(
            new()
            {
                Map = MapType.Malas, Cliloc = 1060643, SelectedCliloc = 1062039,
                Destination =
                [
                    new() { Name = "Luna", Cliloc = 1060641, Location = new Point3D(1015, 527, -65) },
                    new() { Name = "Umbra", Cliloc = 1060642, Location = new Point3D(1997, 1386, -85) }
                ]
            }
        );

        var result = Run(
            """
            local facets = moongates.facets()
            local malas = facets[1]
            local umbra = malas.destinations[2]
            return #facets, malas.map, malas.cliloc, malas.selected_cliloc, #malas.destinations,
                umbra.name, umbra.cliloc, umbra.x, umbra.y, umbra.z
            """
        );

        Assert.Equal(
            [1, (int)MapType.Malas, 1060643, 1062039, 2],
            result[..5].Select(value => value.Read<int>())
        );
        Assert.Equal("Umbra", result[5].Read<string>());
        Assert.Equal([1060642, 1997, 1386, -85], result[6..].Select(value => value.Read<int>()));
    }

    [Fact]
    public void Facets_WithoutMoongates_IsAnEmptyTable()
    {
        Assert.Equal(0, Run("return #moongates.facets()")[0].Read<int>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new MoongatesModule(_moongates));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
