using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Modules;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Scripting;

public class LuaScriptEngineServiceTests
{
    [Test]
    public void AddCallback_AndExecuteCallback_ShouldInvokeCallback()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        object[]? captured = null;

        service.AddCallback("onTest", args => captured = args);
        service.ExecuteCallback("onTest", 1, "two");

        Assert.That(captured, Is.Not.Null);
        Assert.That(captured!.Length, Is.EqualTo(2));
        Assert.That(captured[0], Is.EqualTo(1));
        Assert.That(captured[1], Is.EqualTo("two"));
    }

    [Test]
    public void AddConstant_ShouldExposeNormalizedGlobal()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        service.AddConstant("myValue", 42);
        var result = service.ExecuteFunction("MY_VALUE");

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(42d));
            }
        );
    }

    [Test]
    public void AddManualModuleFunction_ShouldBeCallableFromLua()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        service.AddManualModuleFunction<int, int>("math", "double", static value => value * 2);
        var result = service.ExecuteFunction("math.double(21)");

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(42d));
            }
        );
    }

    [Test]
    public void ExecuteFunction_WhenLuaError_ShouldReturnErrorAndRaiseEvent()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        ScriptErrorInfo? capturedError = null;
        service.OnScriptError += (_, info) => capturedError = info;

        var result = service.ExecuteFunction("unknown_function()");

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.False);
                Assert.That(result.Message, Is.Not.Empty);
                Assert.That(capturedError, Is.Not.Null);
            }
        );
    }

    [Test]
    public void ExecuteScript_WhenScriptIsValid_ShouldReuseCompiledChunkFromCache()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        const string script = "local x = 1 + 1";

        service.ExecuteScript(script);
        service.ExecuteScript(script);

        var metrics = service.GetExecutionMetrics();

        Assert.Multiple(
            () =>
            {
                Assert.That(metrics.CacheHits, Is.EqualTo(1));
                Assert.That(metrics.CacheMisses, Is.EqualTo(1));
                Assert.That(metrics.TotalScriptsCached, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void ExecuteScript_WhenSyntaxIsInvalid_ShouldNotCacheFailedCompilation()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        const string invalidScript = "local x =";

        Assert.That(() => service.ExecuteScript(invalidScript), Throws.Exception);
        Assert.That(() => service.ExecuteScript(invalidScript), Throws.Exception);

        var metrics = service.GetExecutionMetrics();

        Assert.Multiple(
            () =>
            {
                Assert.That(metrics.CacheHits, Is.EqualTo(0));
                Assert.That(metrics.CacheMisses, Is.EqualTo(2));
                Assert.That(metrics.TotalScriptsCached, Is.EqualTo(0));
            }
        );
    }

    [Test]
    public void ExecuteScriptFile_WhenFileMissing_ShouldThrowFileNotFoundException()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);
        var file = Path.Combine(temp.Path, "scripts", "missing.lua");

        Assert.That(() => service.ExecuteScriptFile(file), Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public async Task StartAsync_WithLogModule_ShouldKeepLogAsTable()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = Path.Combine(temp.Path, ".luarc");
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(LogModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var typeResult = service.ExecuteFunction("type(log)");
        var callResult = service.ExecuteFunction("log.info('hello')");

        Assert.Multiple(
            () =>
            {
                Assert.That(typeResult.Success, Is.True);
                Assert.That(typeResult.Data, Is.EqualTo("table"));
                Assert.That(callResult.Success, Is.True);
            }
        );
    }

    [Test]
    public async Task StartAsync_ShouldGenerateDefinitionsAndLuarcUnderConfiguredLuarcDirectory()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = Path.Combine(temp.Path, ".luarc");
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(LogModule))],
            new Container(),
            new LuaEngineConfig(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var definitionsPath = Path.Combine(luarcDir, "definitions.lua");
        var luarcPath = Path.Combine(luarcDir, ".luarc.json");

        Assert.Multiple(
            () =>
            {
                Assert.That(File.Exists(definitionsPath), Is.True);
                Assert.That(File.Exists(luarcPath), Is.True);
            }
        );

        var luarcContent = await File.ReadAllTextAsync(luarcPath);
        Assert.That(luarcContent, Does.Contain(scriptsDir));
        Assert.That(luarcContent, Does.Contain(luarcDir));
    }

    [Test]
    public async Task StartAsync_WithGumpModule_ShouldBuildLayoutFromLua()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = Path.Combine(temp.Path, ".luarc");
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            """
            (function()
                local g = gump.create()
                g:ResizePic(0, 0, 9200, 300, 200)
                g:Text(80, 15, 0x480, "The Blacksmith")
                g:Text(30, 50, 0, "What dost thou require?")
                return g:BuildLayout()
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(
                    result.Data,
                    Is.EqualTo(
                        "{ resizepic 0 0 9200 300 200 } { text 80 15 1152 0 } { text 30 50 0 1 }"
                    )
                );
            }
        );
    }

    [Test]
    public async Task StartAsync_WithGumpModule_ShouldExposeTextEntriesFromLua()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = Path.Combine(temp.Path, ".luarc");
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule))],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            """
            (function()
                local g = gump.create()
                g:Text(10, 10, 0, "First")
                g:Text(10, 25, 0, "Second")
                local texts = g:BuildTexts()
                return texts[1] .. "|" .. texts[2]
            end)()
            """
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo("First|Second"));
            }
        );
    }

    [Test]
    public void ToScriptEngineFunctionName_ShouldConvertToSnakeCase()
    {
        using var temp = new TempDirectory();
        var service = CreateService(temp.Path);

        var name = service.ToScriptEngineFunctionName("HelloWorldMethod");

        Assert.That(name, Is.EqualTo("hello_world_method"));
    }

    private static LuaScriptEngineService CreateService(string rootPath)
    {
        var dirs = new DirectoriesConfig(rootPath, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = Path.Combine(rootPath, ".luarc");
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(luarcDir);

        return new(
            dirs,
            [],
            new Container(),
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );
    }
}
