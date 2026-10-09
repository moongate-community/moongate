using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;

namespace Moongate.Tests.Integration.Server.Ultima.Events;

/// <summary>
///     The shipped
///     <c>
///         trick_or_treat.lua
///     </c>
///     run against stand-ins for the modules it calls (npc, mobile, item,
///     schedule, world, localization), written in Lua at the top of each run.
/// </summary>
public sealed class TrickOrTreatScriptTests
{
    // Records what the script does in the globals log, said and given; math.random answers from the queue.
    private const string Prelude = """
                                   log = { said = {}, given = {}, told = {}, created = {}, props = {} }
                                   active = true
                                   now = 1000
                                   dead = false
                                   queue = {}
                                   math.random = function(a, b)
                                       if #queue > 0 then return table.remove(queue, 1) end
                                       return a
                                   end
                                   schedule = { is_active = function(id) return active and id == "halloween" end }
                                   world = { now = function() return now end }
                                   localization = { get = function(id) return "msg" .. id end }
                                   npc = {
                                       location = function(s) return { map = 1, x = 100, y = 100, z = 0 } end,
                                       look_at = function() end,
                                       say = function(s, t) log.said[#log.said + 1] = t end,
                                       get_prop = function(s, k) return log.props[k] end,
                                       set_prop = function(s, k, v) log.props[k] = v end,
                                   }
                                   mobile = {
                                       location = function(s) return { map = 1, x = far and 150 or 102, y = 100, z = 0 } end,
                                       is_dead = function(s) return dead end,
                                       message = function(s, t) log.told[#log.told + 1] = t end,
                                   }
                                   item = {
                                       give = function(s, t) log.given[#log.given + 1] = t return 1 end,
                                       create = function(t, map, x, y, z) log.created[#log.created + 1] = t return 2 end,
                                   }
                                   """;

    [Fact]
    public void WhileTheEventIsOn_ASaying_GivesACandy_AndTheShopkeeperAnswers()
    {
        var result = Run("return m.listen(1, 2, 'Trick or Treat!'), log.said[1], log.given[1], log.told[1]");

        Assert.True(result[0].Read<bool>());
        Assert.Equal("msg30232", result[1].Read<string>());
        Assert.Equal("0x4690_nougat_swirl", result[2].Read<string>());
        Assert.Equal("msg30234", result[3].Read<string>());
    }

    [Fact]
    public void OutsideTheEvent_NothingHappens()
    {
        var result = Run("active = false return m.listen(1, 2, 'trick or treat'), #log.said, #log.given");

        Assert.False(result[0].Read<bool>());
        Assert.Equal(0, result[1].Read<int>() + result[2].Read<int>());
    }

    [Fact]
    public void OtherWords_AreIgnored()
    {
        Assert.False(Run("return m.listen(1, 2, 'hello')")[0].Read<bool>());
    }

    [Fact]
    public void FromFarAway_OrDead_NothingHappens()
    {
        var far = Run("far = true return m.listen(1, 2, 'trick or treat'), #log.given");
        var dead = Run("dead = true return m.listen(1, 2, 'trick or treat'), #log.given");

        Assert.False(far[0].Read<bool>());
        Assert.Equal(0, far[1].Read<int>());
        Assert.False(dead[0].Read<bool>());
        Assert.Equal(0, dead[1].Read<int>());
    }

    [Fact]
    public void TheShopkeeperRests_ThenGivesAgain()
    {
        var result = Run(
            "queue = { 400 } m.listen(1, 2, 'trick or treat') local first = #log.given "
            + "local again = m.listen(1, 2, 'trick or treat') local rested = #log.given "
            + "now = 1000 + 401 m.listen(1, 2, 'trick or treat') "
            + "return first, rested, log.told[2], #log.given"
        );

        Assert.Equal(1, result[0].Read<int>());
        Assert.Equal(1, result[1].Read<int>());
        Assert.Equal("msg30235", result[2].Read<string>());
        Assert.Equal(2, result[3].Read<int>());
    }

    [Fact]
    public void ATrick_ShoutsAndLeavesBlood_WithoutACandy()
    {
        // The first random is the rest, the second the one-in-ten roll, then the number of splashes (3).
        var result = Run(
            "queue = { 300, 1, 3 } m.listen(1, 2, 'trick or treat') return log.said[1], #log.given, #log.created"
        );

        Assert.Equal("msg30233", result[0].Read<string>());
        Assert.Equal(0, result[1].Read<int>());
        Assert.True(result[2].Read<int>() >= 3);
    }

    private static LuaValue[] Run(string body)
    {
        using var state = LuaState.Create();
        state.OpenStandardLibraries();
        var script = File.ReadAllText(ShippedScript("common/trick_or_treat.lua"));
        var chunk = $"{Prelude}\nm = (function()\n{script}\nend)()\n{body}";

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    private static string ShippedScript(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "moongate_root", "scripts", relativePath);
    }
}
