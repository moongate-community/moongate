using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;

namespace Moongate.Tests.Integration.Server.Ultima.Events;

/// <summary>
///     The shipped
///     <c>
///         christmas.lua
///     </c>
///     run against stand-ins for the modules it calls, written in Lua at the top of
///     each run.
/// </summary>
public sealed class ChristmasScriptTests
{
    private const string Prelude = """
                                   log = { given = {}, told = {}, broadcast = {}, props = {}, decor = {} }
                                   decor = {
                                   place = function(id, templates) log.decor[#log.decor + 1] = 'place:' .. id .. ':' .. #templates end,
                                   remove = function(id) log.decor[#log.decor + 1] = 'remove:' .. id end,
                                   }
                                   require = function(name) return decor end
                                   now = 10000000
                                   full = false
                                   queue = {}
                                   math.random = function(a) if #queue > 0 then return table.remove(queue, 1) end return a end
                                   world = { now = function() return now end, broadcast = function(t) log.broadcast[#log.broadcast + 1] = t end }
                                   localization = { get = function(id) return "msg" .. id end }
                                   item = { give = function(who, template) if full then return nil end log.given[#log.given + 1] = template return 1 end }
                                   mobile = {
                                       get_prop = function(s, k) return log.props[k] end,
                                       set_prop = function(s, k, v) log.props[k] = v end,
                                       message = function(s, t) log.told[#log.told + 1] = t end,
                                   }
                                   """;

    [Fact]
    public void TheFirstLogin_GetsTheGift_AndIsMarked()
    {
        var result = Run(
            "queue = { 10 } m.on_login('christmas', 'Christmas', 2) "
            + "return #log.given, log.given[1], log.given[2], log.given[3], log.given[4], log.told[1], log.props['christmas.gift']"
        );

        Assert.Equal(4, result[0].Read<int>());
        Assert.Equal(
            ["snow_pile", "glacial_snow", "0x236e", "0x2378_a_decorative_topiary"],
            result[1..5].Select(value => value.Read<string>())
        );
        Assert.Equal("msg30240", result[5].Read<string>());
        Assert.Equal(10000000, result[6].Read<int>());
    }

    [Theory,
     InlineData(60, "0x2378_a_decorative_topiary"),
     InlineData(61, "0x2376_a_festive_cactus"),
     InlineData(84, "0x2376_a_festive_cactus"),
     InlineData(85, "0x2377_a_snowy_tree")]
    public void TheDecoration_FollowsTheRoll(int roll, string template)
    {
        var result = Run($"queue = {{ {roll} }} m.on_login('christmas', 'Christmas', 2) return log.given[4]");

        Assert.Equal(template, result[0].Read<string>());
    }

    [Fact]
    public void ASecondLoginOfTheSameSeason_GetsNothing()
    {
        var result = Run(
            "m.on_login('christmas', 'Christmas', 2) now = now + 86400 * 5 m.on_login('christmas', 'Christmas', 2) return #log.given"
        );

        Assert.Equal(4, result[0].Read<int>());
    }

    [Fact]
    public void TheNextYear_GetsANewGift()
    {
        var result = Run(
            "m.on_login('christmas', 'Christmas', 2) now = now + 86400 * 365 m.on_login('christmas', 'Christmas', 2) return #log.given"
        );

        Assert.Equal(8, result[0].Read<int>());
    }

    [Fact]
    public void AFullBackpack_GetsNoGift_AndIsNotMarked()
    {
        var result = Run(
            "full = true m.on_login('christmas', 'Christmas', 2) return #log.given, #log.told, log.props['christmas.gift']"
        );

        Assert.Equal(0, result[0].Read<int>());
        Assert.Equal(0, result[1].Read<int>());
        Assert.Equal(LuaValue.Nil, result[2]);
    }

    [Fact]
    public void TheSeasonIsAnnounced_AtItsStartAndEnd()
    {
        var result = Run(
            "m.on_start('christmas', 'Christmas') m.on_end('christmas', 'Christmas') return log.broadcast[1], log.broadcast[2]"
        );

        Assert.Equal(["msg30238", "msg30239"], result.Select(value => value.Read<string>()));
    }

    [Fact]
    public void TheTowns_AreDecoratedAtTheStart_AndClearedAtTheEnd()
    {
        var result = Run(
            "m.on_start('christmas', 'Christmas') m.on_end('christmas', 'Christmas') return log.decor[1], log.decor[2]"
        );

        Assert.Equal(["place:christmas:4", "remove:christmas"], result.Select(value => value.Read<string>()));
    }

    private static LuaValue[] Run(string body)
    {
        using var state = LuaState.Create();
        state.OpenStandardLibraries();
        var script = File.ReadAllText(ShippedScript("events/christmas.lua"));
        var chunk = $"{Prelude}\n{script}\nm = christmas\n{body}";

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
