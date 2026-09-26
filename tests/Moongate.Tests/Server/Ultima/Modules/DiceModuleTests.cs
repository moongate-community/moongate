using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Ultima.Modules;
using Moongate.Scripting.Utils;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class DiceModuleTests
{
    [Fact]
    public void Roll_StaysWithinTheExpressionBounds()
    {
        var result = Run("local lo, hi = 99, 0 for i = 1, 500 do local v = dice.roll('2d6+3') lo = math.min(lo, v) hi = math.max(hi, v) end return lo, hi");

        Assert.InRange(result[0].Read<int>(), 5, 15);
        Assert.InRange(result[1].Read<int>(), 5, 15);
    }

    [Fact]
    public void Roll_ABadExpression_RaisesALuaErrorNamingIt()
    {
        var result = Run("local ok, err = pcall(dice.roll, '1d6 fire') return ok, err");

        Assert.False(result[0].Read<bool>());
        Assert.Contains("'1d6 fire'", result[1].Read<string>());
    }

    [Fact]
    public void TryRoll_ReturnsTheRollOrNil()
    {
        var result = Run("return dice.try_roll('5'), dice.try_roll('1d0'), dice.try_roll('')");

        Assert.Equal(5, result[0].Read<int>());
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal(LuaValue.Nil, result[2]);
    }

    private static LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenMathLibrary();
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(state, new DiceModule());

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }
}
