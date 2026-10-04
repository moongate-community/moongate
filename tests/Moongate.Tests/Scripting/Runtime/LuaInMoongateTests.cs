using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Runtime;

/// <summary>
///     Pins what docs/scripting/lua-in-moongate.md tells script authors about the Lua of the server, by running its
///     statements through the script engine. A failure here means the page is no longer true: change the page with
///     the engine. The cap of string.rep and where print writes are pinned by LuaScriptEngineServiceTests, and the
///     enum tables by LuaModuleBinderConstantsTests.
/// </summary>
public sealed class LuaInMoongateTests : IDisposable
{
    private readonly TemporaryScriptsDirectory _scripts = new();
    private readonly Container _container = new();
    private readonly StubGameLoop _loop = new();
    private readonly RecordingTimerService _timers = new();

    public LuaInMoongateTests()
    {
        _container.RegisterMoongateEventBus();
        _container.RegisterInstance<IGameLoopService>(_loop);
        _container.RegisterInstance<ITimerService>(_timers);
        _container.AddScriptModule<ProbeModule>();
        _container.AddScriptModule<LogModule>();
    }

    [Fact]
    public async Task TheLanguageIsLua52_WithGoto_AndWithoutThe53OperatorsAndEscapes()
    {
        var values = await Run(
            """
            local function compiles(code) return load(code) ~= nil end
            return _VERSION,
                load("do goto done end ::done:: return 'reached'")(),
                compiles("return 7 // 2"), compiles("return 6 & 3"), compiles("return '\\u{41}'"), compiles("return '\\x41'")
            """
        );

        Assert.Equal(["Lua 5.2", "reached", false, false, false, true], values);
    }

    [Fact]
    public async Task TheLibrariesOfThePage_AreThereAndTheOthersAreNot()
    {
        var values = await Run(
            """
            local present = string ~= nil and table ~= nil and math ~= nil and load ~= nil and require ~= nil
                and table.unpack ~= nil and table.pack ~= nil and select ~= nil and pcall ~= nil and xpcall ~= nil
                and setmetatable ~= nil and coroutine.yield ~= nil and coroutine.running ~= nil and coroutine.status ~= nil
            local absent = io == nil and os == nil and debug == nil and bit32 == nil and utf8 == nil
                and loadstring == nil and unpack == nil and dofile == nil and loadfile == nil and rawset == nil
                and coroutine.create == nil and coroutine.wrap == nil and coroutine.resume == nil
                and package.path == nil and package.cpath == nil and package.loadlib == nil and package.searchpath == nil
            return present, absent
            """
        );

        Assert.Equal([true, true], values);
    }

    [Fact]
    public async Task AHexadecimalNumberBetweenBrackets_DoesNotCompile_ButItsOtherFormsDo()
    {
        var values = await Run(
            """
            local index, index_error = load("local t = {} t[0x0A] = 1")
            local constructor = load("return { [0x0A] = 1 }")
            local wrapped = load("local t = {} t[(0x0A)] = 1 return t[10]")()
            local variable = load("local key = 0x0A local t = {} t[key] = 1 return t[10]")()
            return index == nil, index_error, constructor == nil, wrapped, variable
            """
        );

        Assert.Equal(true, values[0]);
        Assert.Contains("malformed number", (string)values[1]!, StringComparison.Ordinal);
        Assert.Equal([true, 1d, 1d], values[2..]);
    }

    [Fact]
    public async Task StringsCountAndCompareUtf16Units()
    {
        var values = await Run("""return #"è", #"€", string.len("añb"), "é" < "z", "Z" < "a" """);

        Assert.Equal([1d, 1d, 3d, false, true], values);
    }

    [Fact]
    public async Task NumbersAreDoubles_WrittenWithFullPrecision()
    {
        var values = await Run(
            """
            local formatted, format_error = pcall(string.format, "%d", 3.5)
            return tostring(10 / 2), tostring(3 / 2), tostring(0.1 + 0.2), tostring(2 ^ 63),
                string.format("%d", 3), formatted, format_error
            """
        );

        Assert.Equal(["5", "1.5", "0.30000000000000004", "9.223372036854776E+18", "3", false], values[..6]);
        Assert.Contains("no integer representation", (string)values[6]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrintReturnsNothing()
    {
        Assert.Equal([0d], await Run("""return select("#", print("to the server log"))"""));
    }

    [Fact]
    public async Task AModuleTableIsReadOnly_AndItsMetatableIsLocked()
    {
        var values = await Run("""return getmetatable(log), pcall(function() log.extra = 1 end)""");

        Assert.Equal(["locked", false], values[..2]);
        Assert.Contains("'log' is read-only", (string)values[2]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEnumParameter_TakesTheNumberOrTheMemberName_AndRefusesAnotherName()
    {
        var values = await Run(
            """
            local accepted, name_error = pcall(probe.next_colour, "red")
            return probe.next_colour(ProbeColour.Red), probe.next_colour("Red"), probe.next_colour(0), accepted, name_error
            """
        );

        Assert.Equal([1d, 1d, 1d, false], values[..4]);
        Assert.Contains("bad argument #1 to 'probe.next_colour'", (string)values[4]!, StringComparison.Ordinal);
        Assert.Contains("'red' is not a member of ProbeColour", (string)values[4]!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIntegerParameter_NeedsAWholeNumber_AndAMissingArgumentIsNamed()
    {
        var values = await Run(
            """
            local added, add_error = pcall(probe.add, 1.5, 1)
            local greeted, greet_error = pcall(probe.greet)
            return probe.scale(2), probe.add(2, 1), added, add_error, greeted, greet_error
            """
        );

        Assert.Equal([4d, 3d, false], values[..3]);
        Assert.Contains("bad argument #1 to 'probe.add'", (string)values[3]!, StringComparison.Ordinal);
        Assert.Equal(false, values[4]);
        Assert.Contains("bad argument #1 to 'probe.greet' (name is required)", (string)values[5]!, StringComparison.Ordinal);
    }

    private async Task<object?[]> Run(string body)
    {
        _scripts.Write("init.lua", "function statement()\n" + body + "\nend");
        var options = new ScriptEngineOptions
        {
            ScriptsDirectory = _scripts.Path,
            MaxInstructionsPerResume = 20_000,
            MaxInstructionsPerChunk = 100_000,
            HookInterval = 100,
            WriteDefinitions = false
        };
        using var engine = new LuaScriptEngineService(
            options,
            _container.Resolve<IScriptModuleRegistry>(),
            _container,
            _loop,
            _timers,
            new EventBusAdapter(_container)
        );
        await engine.StartAsync();
        var result = engine.Call("statement");
        Assert.Null(result.Error);

        return result.Values.ToArray();
    }

    public void Dispose()
    {
        _container.Dispose();
        _scripts.Dispose();
    }
}
