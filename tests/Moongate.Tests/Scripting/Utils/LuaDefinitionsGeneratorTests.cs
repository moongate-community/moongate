using System.Text.Json;
using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Utils;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Utils;

public sealed class LuaDefinitionsGeneratorTests
{
    private static (List<BoundModule> Modules, List<Type> Enums) BindProbeAndLog()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        var modules = new List<BoundModule> { binder.Bind(state, new ProbeModule()), binder.Bind(state, new LogModule()) };

        foreach (var enumType in binder.DiscoveredEnums)
        {
            binder.BindEnum(state, enumType);
        }

        return (modules, binder.PublishedEnums.ToList());
    }

    [Fact]
    public void Render_DeclaresEachModuleAsAClassWithTypedFunctionsAndConstants()
    {
        var (modules, enums) = BindProbeAndLog();

        var text = LuaDefinitionsGenerator.Render(modules, enums);

        Assert.Contains("---@class probe", text, StringComparison.Ordinal);
        Assert.Contains("---@param left integer", text, StringComparison.Ordinal);
        Assert.Contains("---@param right integer", text, StringComparison.Ordinal);
        Assert.Contains("---@return integer", text, StringComparison.Ordinal);
        Assert.Contains("function probe.add(left, right) end", text, StringComparison.Ordinal);
        Assert.Contains("---@param factor? number", text, StringComparison.Ordinal);
        Assert.Contains("function probe.scale(value, factor) end", text, StringComparison.Ordinal);
        Assert.Contains("---@param extras any", text, StringComparison.Ordinal);
        Assert.Contains("function probe.record(what, ...) end", text, StringComparison.Ordinal);
        Assert.Contains("---@param colour ProbeColour", text, StringComparison.Ordinal);
        Assert.Contains("---@field LEVEL_INFO integer", text, StringComparison.Ordinal);
        Assert.Contains("log.LEVEL_INFO = 2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_DeclaresEnumsAsEnumTables()
    {
        var (modules, enums) = BindProbeAndLog();

        var text = LuaDefinitionsGenerator.Render(modules, enums);

        Assert.Contains("---@enum ProbeColour", text, StringComparison.Ordinal);
        Assert.Contains("Green = 1,", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_IncludesHelpTextAsComments()
    {
        var (modules, enums) = BindProbeAndLog();

        var text = LuaDefinitionsGenerator.Render(modules, enums);

        Assert.Contains("---Adds two integers.", text, StringComparison.Ordinal);
        Assert.Contains("---Exercises every conversion the binder supports.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_DeclaresWait()
    {
        var (modules, enums) = BindProbeAndLog();

        Assert.Contains("function wait(seconds) end", LuaDefinitionsGenerator.Render(modules, enums), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ProducesBothFilesAndAValidLuarc()
    {
        using var scripts = new TemporaryScriptsDirectory();
        var (modules, enums) = BindProbeAndLog();

        LuaDefinitionsGenerator.Write(scripts.Path, modules, enums);

        Assert.True(File.Exists(Path.Combine(scripts.Path, "definitions.lua")));
        using var luarc = JsonDocument.Parse(File.ReadAllText(Path.Combine(scripts.Path, ".luarc.json")));
        Assert.Equal("Lua 5.2", luarc.RootElement.GetProperty("runtime.version").GetString());
        var globals = luarc.RootElement.GetProperty("diagnostics.globals").EnumerateArray().Select(element => element.GetString()).ToList();
        Assert.Contains("probe", globals);
        Assert.Contains("log", globals);
        Assert.Contains("ProbeColour", globals);
        Assert.Contains("wait", globals);
        Assert.Contains("definitions.lua", luarc.RootElement.GetProperty("workspace.library").EnumerateArray().Select(element => element.GetString()!));
    }

    [Fact]
    public void Write_IsIdempotent()
    {
        using var scripts = new TemporaryScriptsDirectory();
        var (modules, enums) = BindProbeAndLog();

        LuaDefinitionsGenerator.Write(scripts.Path, modules, enums);
        var first = File.ReadAllText(Path.Combine(scripts.Path, "definitions.lua"));
        LuaDefinitionsGenerator.Write(scripts.Path, modules, enums);

        Assert.Equal(first, File.ReadAllText(Path.Combine(scripts.Path, "definitions.lua")));
    }
}
