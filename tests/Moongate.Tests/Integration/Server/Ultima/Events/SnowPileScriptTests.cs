using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;

namespace Moongate.Tests.Integration.Server.Ultima.Events;

/// <summary>
///     The shipped
///     <c>
///         snow_pile.lua
///     </c>
///     run against stand-ins for the modules it calls, written in Lua at the top of
///     each run.
/// </summary>
public sealed class SnowPileScriptTests
{
    // User 2 holds the pile 1 and throws at 3, who carries snow; log records what the script does.
    private const string Prelude = """
                                   log = { told = {}, thrown = {}, sounds = 0, animations = 0, mprops = {} }
                                   now = 1000
                                   mounted = false
                                   owner = 2
                                   pick = nil
                                   carries = { [3] = true }
                                   place = { [2] = { map = 1, x = 100, y = 100 }, [3] = { map = 1, x = 104, y = 100 } }
                                   world = { now = function() return now end }
                                   target = { pick = function(user, callback) pick = callback return true end }
                                   item = {
                                       owner = function(s) return owner end,
                                       find = function(who, template) if carries[who] and template == "snow_pile" then return { 9 } end return {} end,
                                   }
                                   mobile = {
                                       is_mounted = function(s) return mounted end,
                                       location = function(s) return place[s] end,
                                       message_cliloc = function(s, c) log.told[#log.told + 1] = s .. ":" .. c end,
                                       get_prop = function(s, k) return log.mprops[k] end,
                                       set_prop = function(s, k, v) log.mprops[k] = v end,
                                       play_sound = function(s, n) log.sounds = log.sounds + 1 end,
                                       animate = function(s, a) log.animations = log.animations + 1 end,
                                   }
                                   effect = { moving = function(a, b, g, o) log.thrown[#log.thrown + 1] = a .. ">" .. b .. ":" .. g end }
                                   """;

    [Fact]
    public void ThePile_AsksForATarget_AndTheSnowballHits()
    {
        var result = Run(
            "local used = m.on_use(1, 2) pick({ kind = 'object', serial = 3 }) "
            + "return used, log.told[1], log.told[2], log.told[3], log.thrown[1], log.sounds, log.animations"
        );

        Assert.True(result[0].Read<bool>());
        Assert.Equal("2:1005575", result[1].Read<string>());
        Assert.Equal("3:1010572", result[2].Read<string>());
        Assert.Equal("2:1010573", result[3].Read<string>());
        Assert.Equal("2>3:14052", result[4].Read<string>());
        Assert.Equal(1, result[5].Read<int>());
        Assert.Equal(1, result[6].Read<int>());
    }

    [Fact]
    public void ThePileNotInTheBackpack_SaysSo()
    {
        var result = Run("owner = 7 m.on_use(1, 2) return log.told[1], pick");

        Assert.Equal("2:1042010", result[0].Read<string>());
        Assert.Equal(LuaValue.Nil, result[1]);
    }

    [Fact]
    public void WhileMounted_NoSnowball()
    {
        var result = Run("mounted = true m.on_use(1, 2) return log.told[1], pick");

        Assert.Equal("2:1010097", result[0].Read<string>());
        Assert.Equal(LuaValue.Nil, result[1]);
    }

    [Fact]
    public void AtYourself_ANoAnswer_OrFarAway_NothingFlies()
    {
        var self = Run("m.on_use(1, 2) pick({ kind = 'object', serial = 2 }) return log.told[2], #log.thrown");
        var empty = Run("m.on_use(1, 2) pick({ kind = 'object', serial = 4 }) return log.told[2], #log.thrown");
        var far = Run(
            "place[3].x = 130 m.on_use(1, 2) pick({ kind = 'object', serial = 3 }) return log.told[2], #log.thrown"
        );

        Assert.Equal("2:1005576", self[0].Read<string>());
        Assert.Equal("2:1005577", empty[0].Read<string>());
        Assert.Equal("2:500446", far[0].Read<string>());
        Assert.Equal(0, self[1].Read<int>() + empty[1].Read<int>() + far[1].Read<int>());
    }

    [Fact]
    public void ACanceledCursor_DoesNothing()
    {
        var result = Run("m.on_use(1, 2) pick({ kind = 'canceled', reason = 'canceled' }) return #log.thrown, #log.told");

        Assert.Equal(0, result[0].Read<int>());
        Assert.Equal(1, result[1].Read<int>());
    }

    [Fact]
    public void ThePlayerWaitsFiveSeconds_BetweenTwoSnowballs()
    {
        var result = Run(
            "m.on_use(1, 2) pick({ kind = 'object', serial = 3 }) m.on_use(1, 2) local early = log.told[#log.told] "
            + "now = 1006 m.on_use(1, 2) pick({ kind = 'object', serial = 3 }) return early, #log.thrown"
        );

        Assert.Equal("2:1005574", result[0].Read<string>());
        Assert.Equal(2, result[1].Read<int>());
    }

    private static LuaValue[] Run(string body)
    {
        using var state = LuaState.Create();
        state.OpenStandardLibraries();
        var script = File.ReadAllText(ShippedScript("items/snow_pile.lua"));
        var chunk = $"{Prelude}\n{script}\nm = snow_pile\n{body}";

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
