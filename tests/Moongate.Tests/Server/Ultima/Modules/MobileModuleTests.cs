using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class MobileModuleTests
{
    private readonly RecordingSpeechService _speech = new();
    private readonly RecordingTeleportService _teleports = new();
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly MobileEntity _aria = new()
    {
        Id = new Serial(2), Name = "Aria", AccountId = new Serial(0x42), Map = MapType.Felucca,
        Location = new Point3D(1601, 1600, 5)
    };

    public MobileModuleTests()
    {
        _mobiles.EnterWorld(_aria);
    }

    [Fact]
    public void Teleport_AMobileInTheWorld_AsksForTheTeleport()
    {
        var result = Run("return mobile.teleport(2, 5690, 569, 25)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, new Point3D(5690, 569, 25)), Assert.Single(_teleports.Teleports));
    }

    [Fact]
    public void Teleport_Refused_IsFalse()
    {
        _teleports.Result = false;

        Assert.False(Run("return mobile.teleport(2, 5690, 569, 25)")[0].Read<bool>());
    }

    [Theory,
     InlineData("return mobile.teleport(999, 5690, 569, 25)"),
     InlineData("return mobile.teleport(-1, 5690, 569, 25)"),
     InlineData("return mobile.teleport(2, 5690, 569, 128)"),
     InlineData("return mobile.teleport(2, 5690, 569, -129)")]
    public void Teleport_AnUnknownMobileOrAHeightOutOfRange_IsFalseAndAsksNothing(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_teleports.Teleports);
    }

    [Fact]
    public void Location_GivesWhereTheMobileStands()
    {
        var result = Run("local at = mobile.location(2) return at.x, at.y, at.z, at.map");

        Assert.Equal([1601, 1600, 5, (int)MapType.Felucca], result.Select(value => value.Read<int>()));
    }

    [Fact]
    public void Location_OfAnUnknownMobile_IsNil()
    {
        Assert.Equal(LuaValueType.Nil, Run("return mobile.location(999)")[0].Type);
    }

    [Fact]
    public void PlaySound_PlaysItWhereTheMobileStands()
    {
        var result = Run("return mobile.play_sound(2, 0x1FE)");

        Assert.True(result[0].Read<bool>());
        Assert.Equal((_aria, 0x1FE), Assert.Single(_speech.Sounds));
    }

    [Theory,
     InlineData("return mobile.play_sound(999, 0x1FE)"),
     InlineData("return mobile.play_sound(2, -1)"),
     InlineData("return mobile.play_sound(2, 65536)")]
    public void PlaySound_AnUnknownMobileOrSound_IsFalseAndSilent(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_speech.Sounds);
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new MobileModule(_mobiles, _teleports, _speech));

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
