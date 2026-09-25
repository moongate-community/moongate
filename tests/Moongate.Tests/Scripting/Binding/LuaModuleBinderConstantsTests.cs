using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Binding;

public sealed class LuaModuleBinderConstantsTests : IDisposable
{
    private readonly LuaState _state = LuaState.Create();
    private readonly LuaModuleBinder _binder = new(NoThreadGuard.Instance);

    public LuaModuleBinderConstantsTests()
    {
        _state.OpenBasicLibrary();
    }

    [Fact]
    public void BindEnum_OnATypeThatIsNotAnEnum_IsRefusedByName()
    {
        var exception = Assert.Throws<ArgumentException>(() => _binder.BindEnum(_state, typeof(int)));

        Assert.Contains("System.Int32 is not an enum", exception.Message, StringComparison.Ordinal);
        Assert.True(_state.Environment["Int32"].Type == LuaValueType.Nil);
    }

    [Fact]
    public void BindEnum_PublishesAReadOnlyTableOfMembers()
    {
        _binder.BindEnum(_state, typeof(ProbeColour));

        var result = Run("return ProbeColour.Red, ProbeColour.Green, ProbeColour.Blue");

        Assert.Equal(0, result[0].Read<double>());
        Assert.Equal(1, result[1].Read<double>());
        Assert.Equal(2, result[2].Read<double>());
        Assert.Throws<LuaRuntimeException>(() => Run("ProbeColour.Red = 5"));
    }

    [Fact]
    public void Bind_AssigningAConstantFromLua_Raises()
    {
        _binder.Bind(_state, new LimitsModule());

        var exception = Assert.Throws<LuaRuntimeException>(() => Run("limits.MAX_PLAYERS = 1"));

        Assert.Contains("read-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_ConstantAndFunctionSharingAName_IsABindingError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _binder.Bind(_state, new DuplicateNameModule()));

        Assert.Contains("'same'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_ConstantOfAnEnumType_IsDiscoveredForEnumPublication()
    {
        _binder.Bind(_state, new LimitsModule());

        Assert.Contains(typeof(ProbeColour), _binder.DiscoveredEnums);
    }

    [Fact]
    public void Bind_ConstantOfAnUnsupportedType_IsABindingError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _binder.Bind(_state, new BrokenConstantModule()));

        Assert.Contains("NOW", exception.Message, StringComparison.Ordinal);
        Assert.Contains("DateTime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_ConstantThatIsNotStatic_IsABindingError()
    {
        Assert.Throws<InvalidOperationException>(() => _binder.Bind(_state, new InstanceConstantModule()));
    }

    [Fact]
    public void Bind_ConstantWhoseGetterThrows_NamesTheMemberAndKeepsTheCause()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _binder.Bind(_state, new ThrowingConstantModule()));

        Assert.Contains("ThrowingConstantModule.Broken", exception.Message, StringComparison.Ordinal);
        Assert.Contains("the value is not available yet", exception.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Fact]
    public void Bind_ConstantWithASetter_IsABindingError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _binder.Bind(_state, new SettableConstantModule()));

        Assert.Contains("SettableConstantModule.Mutable", exception.Message, StringComparison.Ordinal);
        Assert.Contains("get-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bind_ExposesEveryConstantTypeByValue()
    {
        var bound = _binder.Bind(_state, new LimitsModule());

        var result = Run("return limits.MAX_PLAYERS, limits.version, limits.RATIO, limits.DEBUG, limits.DEFAULT_COLOUR");

        Assert.Equal(250, result[0].Read<double>());
        Assert.Equal("1.2.3", result[1].Read<string>());
        Assert.Equal(0.5, result[2].Read<double>());
        Assert.True(result[3].Read<bool>());
        Assert.Equal(1, result[4].Read<double>());
        Assert.Equal(5, bound.Constants.Count);
        Assert.Contains(bound.Constants, constant => constant.LuaName == "version" && (string?)constant.Value == "1.2.3");
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
