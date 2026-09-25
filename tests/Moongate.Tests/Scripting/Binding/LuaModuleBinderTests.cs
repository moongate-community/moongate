using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Binding;

public sealed class LuaModuleBinderTests : IDisposable
{
    private readonly LuaState _state = LuaState.Create();
    private readonly ProbeModule _module = new();
    private readonly LuaModuleBinder _binder = new(NoThreadGuard.Instance);

    public LuaModuleBinderTests()
    {
        _state.OpenBasicLibrary();
        _binder.Bind(_state, _module);
    }

    [Fact]
    public void Bind_CallsTheThreadGuardOnEveryFunctionCall()
    {
        var guard = new CountingThreadGuard();
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        new LuaModuleBinder(guard).Bind(state, new ProbeModule());

        SyncValueTask.Run(state.DoStringAsync("probe.add(1, 1) probe.add(2, 2)", "test"));

        Assert.Equal(2, guard.Calls);
        Assert.Equal("probe.add", guard.LastMember);
    }

    [Fact]
    public void Bind_ConvertsBoolLongAndTables()
    {
        var result = Run("return probe.flip(true), probe.big(3000000000), probe.pair('x', 4).x, probe.count({1, 2, 3})");

        Assert.False(result[0].Read<bool>());
        Assert.Equal(6000000000, result[1].Read<double>());
        Assert.Equal(4, result[2].Read<double>());
        Assert.Equal(3, result[3].Read<double>());
    }

    [Fact]
    public void Bind_DoesNotExposeMethodsWithoutTheAttribute()
    {
        Assert.Equal(LuaValueType.Nil, Run("return probe.not_exposed")[0].Type);
    }

    [Fact]
    public void Bind_EnumArgumentsAcceptNumberOrName_AndReturnAsNumber()
    {
        Assert.Equal(1, Run("return probe.next_colour(0)")[0].Read<double>());
        Assert.Equal(0, Run("return probe.next_colour('Blue')")[0].Read<double>());
    }

    [Fact]
    public void Bind_ExceptionInsideTheModule_SurfacesAsALuaErrorWithTheMessage()
    {
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        binder.Bind(_state, new ThrowingModule());

        var exception = Assert.Throws<LuaRuntimeException>(() => Run("return thrower.fail()"));

        Assert.Contains("deliberate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_HonoursTheNameOverride()
    {
        Assert.Equal(6, Run("return probe.scale(3)")[0].Read<double>());
        Assert.Equal(9, Run("return probe.scale(3, 3)")[0].Read<double>());
    }

    [Fact]
    public void Bind_IntegerOutOfRange_RaisesALuaErrorInsteadOfOverflowing()
    {
        var exception = Assert.Throws<LuaRuntimeException>(() => Run("return probe.add(9999999999, 1)"));

        Assert.Contains("out of range", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_LongAtTwoToThe63_IsOutOfRangeInsteadOfWrapping()
    {
        // long.MaxValue has no exact double, so a bound of "<= long.MaxValue" rounds up to 2^63 and lets
        // exactly 2^63 through, where the cast wraps it to long.MinValue.
        var exception = Assert.Throws<LuaRuntimeException>(() => Run("return probe.big(2^63)"));

        Assert.Contains("out of range", exception.Message, StringComparison.Ordinal);

        // -2^63 is exactly long.MinValue and stays accepted; doubling it wraps to zero.
        Assert.Equal(0, Run("return probe.big(-(2^63))")[0].Read<double>());
    }

    [Fact]
    public void Bind_MissingRequiredArgument_RaisesALuaError()
    {
        Assert.Throws<LuaRuntimeException>(() => Run("return probe.greet()"));
    }

    [Fact]
    public void Bind_ModuleTable_RejectsWritesAndMetatableChanges()
    {
        // rawset bypasses the proxy's __newindex and would shadow a bound function; the binder cannot stop
        // it, so the engine removes rawset from the sandbox. See
        // LuaScriptEngineServiceTests.ModuleTable_CannotBeShadowedByRawset_BecauseTheEngineRemovesIt.
        Assert.Throws<LuaRuntimeException>(() => Run("probe.add = nil"));
        Assert.Throws<LuaRuntimeException>(() => Run("probe.extra = 1"));
        Assert.Throws<LuaRuntimeException>(() => Run("setmetatable(probe, {})"));
    }

    [Fact]
    public void Bind_ParamsCollectsTheRemainingArguments()
    {
        Run("probe.record('a') probe.record('b', 1, nil, 'two', true)");

        Assert.Equal(["a:", "b:1,nil,two,True"], _module.Calls);
    }

    [Fact]
    public void Bind_PublishesTheModuleUnderItsName_WithSnakeCaseFunctionNames()
    {
        var result = Run("return probe.add(2, 3), probe.next_colour(1), probe.greet('Lua')");

        Assert.Equal(5, result[0].Read<double>());
        Assert.Equal(2, result[1].Read<double>());
        Assert.Equal("Hello, Lua", result[2].Read<string>());
    }

    [Fact]
    public void Bind_ReportsTheDiscoveredEnums()
    {
        Assert.Contains(typeof(ProbeColour), _binder.DiscoveredEnums);
    }

    [Fact]
    public void Bind_WrongArgumentType_RaisesALuaErrorNamingTheFunction()
    {
        var exception = Assert.Throws<LuaRuntimeException>(() => Run("return probe.add('x', 1)"));

        Assert.Contains("probe.add", exception.Message, StringComparison.Ordinal);
        Assert.Contains("argument #1", exception.Message, StringComparison.Ordinal);
    }

    private LuaValue[] Run(string source)
    {
        return SyncValueTask.Run(_state.DoStringAsync(source, "test"));
    }

    public void Dispose()
    {
        _state.Dispose();
    }
}
