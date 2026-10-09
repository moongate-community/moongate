using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;

namespace Moongate.Tests.Integration.Server.Ultima.Events;

/// <summary>
///     The shipped
///     <c>
///         halloween.lua
///     </c>
///     hooks run against stand-ins for the modules they call.
/// </summary>
public sealed class HalloweenScriptTests
{
    private const string Prelude = """
                                   log = { broadcast = {}, decor = {} }
                                   decor = {
                                       place = function(id, templates) log.decor[#log.decor + 1] = 'place:' .. id .. ':' .. #templates end,
                                       remove = function(id) log.decor[#log.decor + 1] = 'remove:' .. id end,
                                   }
                                   require = function(name) return decor end
                                   world = { broadcast = function(t) log.broadcast[#log.broadcast + 1] = t end }
                                   localization = { get = function(id) return "msg" .. id end }
                                   """;

    [Fact]
    public void TheStart_DecoratesTheTowns_AndTellsEverybody()
    {
        var result = Run("halloween.on_start('halloween', 'Halloween') return log.decor[1], log.broadcast[1]");

        Assert.Equal("place:halloween:7", result[0].Read<string>());
        Assert.Equal("msg30236", result[1].Read<string>());
    }

    [Fact]
    public void TheEnd_ClearsTheDecorations_AndTellsEverybody()
    {
        var result = Run("halloween.on_end('halloween', 'Halloween') return log.decor[1], log.broadcast[1]");

        Assert.Equal("remove:halloween", result[0].Read<string>());
        Assert.Equal("msg30237", result[1].Read<string>());
    }

    private static LuaValue[] Run(string body)
    {
        using var state = LuaState.Create();
        state.OpenStandardLibraries();
        var script = File.ReadAllText(ShippedScript("events/halloween.lua"));

        return SyncValueTask.Run(state.DoStringAsync($"{Prelude}\n{script}\n{body}", "t"));
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
