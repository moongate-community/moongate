using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Utils;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Targeting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class TargetModuleTests : IAsyncLifetime
{
    private readonly StubTargetService _targets = new();
    private readonly FakeScriptEngine _engine = new() { CurrentScript = "items/bandage.lua" };
    private readonly StubGameLoop _loop = new() { DeferTryPost = true };

    private BroadcastFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        await _fixture.AddAsync(2);
    }

    [Fact]
    public void Pick_AnObject_GivesItsSerialToTheFunction()
    {
        _targets.Result = TargetResult.ForObject(new Serial(0x40000010));

        Assert.True(Run("return target.pick(2, function(picked) end)")[0].Read<bool>());

        var call = Assert.Single(_engine.FunctionCalls);
        Assert.Equal("items/bandage.lua", call.Owner);
        var picked = Assert.IsType<LuaTable>(Assert.Single(call.Args));
        Assert.Equal(("object", 0x40000010L), (picked["kind"].Read<string>(), picked["serial"].Read<long>()));
    }

    [Fact]
    public void PickLocation_APlace_GivesItsMapAndCoordinates()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Felucca, new Point3D(1500, 1600, -5));

        Assert.True(Run("return target.pick_location(2, function(picked) end)")[0].Read<bool>());

        var picked = Assert.IsType<LuaTable>(Assert.Single(Assert.Single(_engine.FunctionCalls).Args));
        Assert.Equal("location", picked["kind"].Read<string>());
        Assert.Equal(
            [(int)MapType.Felucca, 1500, 1600, -5, 0],
            new[] { "map", "x", "y", "z", "graphic" }.Select(key => picked[key].Read<int>())
        );
    }

    [Fact]
    public void PickLocation_AStatic_GivesItsGraphic_SoAScriptCanTellATree()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Felucca, new Point3D(1500, 1600, 20), 0x0CDD);

        Run("target.pick_location(2, function(picked) end)");

        var picked = Assert.IsType<LuaTable>(Assert.Single(Assert.Single(_engine.FunctionCalls).Args));
        Assert.Equal(0x0CDD, picked["graphic"].Read<int>());
    }

    [Fact]
    public void PickLocation_GivesTheLandOfTheCell_SoAScriptCanTellRock()
    {
        _targets.Result = TargetResult.ForLocation(MapType.Felucca, new Point3D(1500, 1600, 20), 0, 0x00E4);

        Run("target.pick_location(2, function(picked) end)");

        var picked = Assert.IsType<LuaTable>(Assert.Single(Assert.Single(_engine.FunctionCalls).Args));
        Assert.Equal((0, 0x00E4), (picked["graphic"].Read<int>(), picked["land"].Read<int>()));
    }

    [Theory,
     InlineData(TargetCancelType.Canceled, "canceled"),
     InlineData(TargetCancelType.Overridden, "overridden"),
     InlineData(TargetCancelType.Disconnected, "disconnected")]
    public void Pick_Canceled_TellsTheFunction_AndWhy(TargetCancelType reason, string told)
    {
        _targets.Result = TargetResult.Canceled(reason);

        Run("target.pick(2, function(picked) end)");

        var picked = Assert.IsType<LuaTable>(Assert.Single(Assert.Single(_engine.FunctionCalls).Args));
        Assert.Equal(("canceled", told), (picked["kind"].Read<string>(), picked["reason"].Read<string>()));
    }

    [Fact]
    public void AnAnswerThatComesWhileAScriptRuns_WaitsForTheNextTurnOfTheLoop()
    {
        // As when a script asks for a second cursor: the first one is canceled inside that script.
        _engine.IsRunningScript = true;
        _targets.Result = TargetResult.Canceled(TargetCancelType.Overridden);

        Run("target.pick(2, function(picked) end)");

        Assert.Empty(_engine.FunctionCalls);
        Assert.Equal(1, _loop.PostedWorkItems);
    }

    [Theory,
     InlineData("return target.pick(999, function() end)"),
     InlineData("return target.pick_location(-1, function() end)"),
     InlineData("return target.cancel(999)")]
    public void APlayerNotInTheWorld_IsFalse_AndNothingIsAsked(string chunk)
    {
        Assert.False(Run(chunk)[0].Read<bool>());

        Assert.Empty(_engine.FunctionCalls);
    }

    [Fact]
    public void Pick_WithoutAFunction_IsAnArgumentError()
    {
        Assert.ThrowsAny<Exception>(() => Run("return target.pick(2, 5)"));
    }

    [Fact]
    public void Cancel_APlayerInTheWorld_IsTrue()
    {
        Assert.True(Run("return target.cancel(2)")[0].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(
            state,
            new TargetModule(_targets, _fixture.Sessions, new Lazy<IScriptEngine>(() => _engine), _loop)
        );

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
