using BenchmarkDotNet.Attributes;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Services;

namespace Moongate.Benchmarks;

[MemoryDiagnoser]
public class LuaScriptEngineBenchmark
{
    private const string SimpleScript = "local x = 1 + 1";
    private const string LoopScript = "local sum = 0; for i = 1, 100 do sum = sum + i end";

    private LuaScriptEngineService _engine = null!;
    private string _tempDir = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "moongate_bench_" + Guid.NewGuid().ToString("N"));

        var dirConfig = new DirectoriesConfig(_tempDir, "scripts", "luarc");
        var engineConfig = new LuaEngineConfig(
            Path.Combine(_tempDir, "luarc"),
            Path.Combine(_tempDir, "scripts"),
            "1.0.0"
        );

        _engine = new LuaScriptEngineService(
            dirConfig,
            [],
            new Container(),
            engineConfig
        );

        _engine.ExecuteScript("function simple_func() return 42 end");
        _engine.ExecuteScript("function add_func(a, b) return a + b end");

        _engine.ExecuteScript(SimpleScript);
        _engine.ExecuteScript(LoopScript);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _engine.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Benchmark]
    public void ExecuteSimpleScriptCached()
        => _engine.ExecuteScript(SimpleScript);

    [Benchmark]
    public void ExecuteLoopScriptCached()
        => _engine.ExecuteScript(LoopScript);

    [IterationSetup(Target = nameof(ExecuteSimpleScriptUncached))]
    public void SetupUncached() => _engine.ClearScriptCache();

    [Benchmark]
    public void ExecuteSimpleScriptUncached()
        => _engine.ExecuteScript(SimpleScript);

    [Benchmark]
    public void CallFunctionNoArgs()
        => _engine.CallFunction("simple_func");

    [Benchmark]
    public void CallFunctionWithArgs()
        => _engine.CallFunction("add_func", 10, 20);
}
