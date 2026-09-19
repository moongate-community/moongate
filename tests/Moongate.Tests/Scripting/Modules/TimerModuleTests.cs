using Lua;
using Lua.Standard;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Modules;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Modules;

public sealed class TimerModuleTests : IDisposable
{
    private readonly LuaState _state = LuaState.Create();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorInfo> _errors = [];
    private readonly ScriptOwnership _ownership = new();
    private string? _owner = "init.lua";

    public TimerModuleTests()
    {
        _state.OpenBasicLibrary();
        _state.OpenCoroutineLibrary();
        var budget = new InstructionBudget(_state, maxInstructionsPerResume: 10_000, maxInstructionsPerChunk: 10_000, hookInterval: 100);
        budget.Install();
        var scheduler = new CoroutineScheduler(_state, _timers, budget, _ownership, _errors.Add, () => _owner);
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(_state, new TimerModule(_timers, scheduler, _ownership));
        Run("function wait(s) return coroutine.yield('wait', s) end");
    }

    private LuaValue[] Run(string source)
    {
        return SyncValueTask.Run(_state.DoStringAsync(source, "test", default));
    }

    [Fact]
    public void After_RegistersAOneShotTimer_AndRunsTheFunctionAsACoroutineWhenItFires()
    {
        Run("handle = timer.after(1.5, function() fired = true wait(1) after_wait = true end)");

        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromSeconds(1.5), timer.Interval);
        Assert.False(timer.Repeat);
        Assert.Equal(LuaValueType.String, _state.Environment["handle"].Type);

        _timers.Fire(timer.Id);

        Assert.True(_state.Environment["fired"].Read<bool>());
        Assert.Equal(LuaValueType.Nil, _state.Environment["after_wait"].Type);
        _timers.Fire(Assert.Single(_timers.Timers).Id);
        Assert.True(_state.Environment["after_wait"].Read<bool>());
    }

    [Fact]
    public void Every_RegistersARepeatingTimer()
    {
        Run("timer.every(2, function() ticks = (ticks or 0) + 1 end)");

        var timer = Assert.Single(_timers.Timers);
        Assert.True(timer.Repeat);
        _timers.Fire(timer.Id);
        _timers.Fire(timer.Id);
        Assert.Equal(2, _state.Environment["ticks"].Read<double>());
    }

    [Fact]
    public void Cancel_UnregistersByHandle_AndReportsWhetherItExisted()
    {
        Run("h = timer.after(5, function() end) first = timer.cancel(h) second = timer.cancel(h)");

        Assert.Empty(_timers.Timers);
        Assert.True(_state.Environment["first"].Read<bool>());
        Assert.False(_state.Environment["second"].Read<bool>());
    }

    [Fact]
    public void Timers_AreOwnedByTheFileThatCreatedThem()
    {
        Run("timer.after(1, function() end)");

        Assert.Equal([_timers.Timers.Single().Id], _ownership.ReleaseTimers("init.lua"));
    }

    [Fact]
    public void After_WithANonFunction_RaisesALuaError()
    {
        Assert.Throws<LuaRuntimeException>(() => Run("timer.after(1, 'nope')"));
    }

    [Fact]
    public void After_FromInsideATimerDrivenCoroutine_InheritsTheStartingFilesOwner()
    {
        Run("timer.after(1, function() timer.after(2, function() end) end)");
        _owner = null;

        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Single(_timers.Timers);
        Assert.Single(_ownership.ReleaseTimers("init.lua"));
    }

    [Fact]
    public void After_OutsideAFile_UsesTheAnonymousOwner()
    {
        _owner = null;

        Run("timer.after(1, function() end)");

        Assert.Single(_ownership.ReleaseTimers("<anonymous>"));
    }

    public void Dispose()
    {
        _state.Dispose();
    }
}
