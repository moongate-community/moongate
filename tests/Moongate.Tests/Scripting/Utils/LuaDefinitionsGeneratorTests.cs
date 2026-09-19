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
    private static List<BoundModule> Modules()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);

        return BindProbeAndLogModules(state, binder);
    }

    private static (List<BoundModule> Modules, List<Type> Enums) BindProbeAndLog()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var binder = new LuaModuleBinder(NoThreadGuard.Instance);
        var modules = BindProbeAndLogModules(state, binder);

        foreach (var enumType in binder.DiscoveredEnums)
        {
            binder.BindEnum(state, enumType);
        }

        return (modules, binder.PublishedEnums.ToList());
    }

    private static List<BoundModule> BindProbeAndLogModules(LuaState state, LuaModuleBinder binder)
    {
        return [binder.Bind(state, new ProbeModule()), binder.Bind(state, new LogModule())];
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
        Assert.Contains("---@param ... any", text, StringComparison.Ordinal);
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

    [Fact]
    public void Render_IsIndependentOfInputOrder()
    {
        var modules = Modules();
        var enums = new List<Type> { typeof(ProbeColour) };
        var forward = LuaDefinitionsGenerator.Render(modules, enums);
        var backward = LuaDefinitionsGenerator.Render(modules.AsEnumerable().Reverse().ToList(), enums.AsEnumerable().Reverse().ToList());
        Assert.Equal(forward, backward);
    }

    [Fact]
    public void Render_EmptyInput_StillDeclaresWait()
    {
        var text = LuaDefinitionsGenerator.Render([], []);
        Assert.Contains("function wait(seconds) end", text, StringComparison.Ordinal);
        Assert.DoesNotContain("---@class", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_EscapesControlCharactersInStringConstants()
    {
        var module = new BoundModule(
            "probe",
            null,
            typeof(ProbeModule),
            new LuaTable(),
            [],
            [new BoundConstant("BANNER", typeof(string), "line one\nline \"two\"\t\\", null)]
        );
        var text = LuaDefinitionsGenerator.Render([module], []);
        Assert.Contains("BANNER = \"line one\\nline \\\"two\\\"\\t\\\\\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PrefixesEveryHelpLine()
    {
        var module = new BoundModule("probe", "first line\nsecond line", typeof(ProbeModule), new LuaTable(), [], []);
        var text = LuaDefinitionsGenerator.Render([module], []);
        Assert.Contains("---first line\n---second line\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_IsIdempotent_ForTheLuarcFileToo()
    {
        using var scripts = new TemporaryScriptsDirectory();
        LuaDefinitionsGenerator.Write(scripts.Path, Modules(), [typeof(ProbeColour)]);
        var first = File.ReadAllText(Path.Combine(scripts.Path, ".luarc.json"));
        LuaDefinitionsGenerator.Write(scripts.Path, Modules(), [typeof(ProbeColour)]);
        Assert.Equal(first, File.ReadAllText(Path.Combine(scripts.Path, ".luarc.json")));
    }
}
