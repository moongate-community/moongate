using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Mounts;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Ultima.Types;
using Moongate.Server.Ultima.Services;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MountModuleTests
{
    private readonly RecordingMountService _mounts = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly ItemService _items = TestItems.Create();

    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca,
        Location = new Point3D(1600, 1600, 0)
    };

    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x100), Name = "an orc", TemplateId = "orc", Map = MapType.Felucca,
        Location = new Point3D(1601, 1600, 0)
    };

    private readonly ItemEntity _statuette = new()
        { Id = new Serial(0x40000600), TemplateId = "ethereal_horse_statue", ItemId = 0x20DD, Amount = 1 };

    public MountModuleTests()
    {
        _mobiles.EnterWorld(_aria);
        _mobiles.EnterWorld(_orc);
        _items.Add([_statuette]);
    }

    [Fact]
    public void RideEthereal_APlayerAndAStatuette_AsksTheMountService_AndAnswersItsResult()
    {
        var result = Run("return mount.ride_ethereal(2, 0x40000600)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, _statuette), Assert.Single(_mounts.Ethereals));
    }

    [Fact]
    public void RideEthereal_WhenTheServiceRefuses_IsFalse()
    {
        _mounts.Accepts = false;

        Assert.False(Run("return mount.ride_ethereal(2, 0x40000600)")[0].Read<bool>());
    }

    [Fact]
    public void RideEthereal_AnNpcAnUnknownPlayerOrAnUnknownItem_IsFalse_WithoutAskingTheService()
    {
        var result = Run("return mount.ride_ethereal(0x100, 0x40000600), mount.ride_ethereal(0x999, 0x40000600), mount.ride_ethereal(2, 0x40000999), mount.ride_ethereal(0, 0)");

        Assert.Equal([false, false, false, false], result.Select(value => value.Read<bool>()));
        Assert.Empty(_mounts.Ethereals);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new MountModule(_mounts, _mobiles, _items));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
