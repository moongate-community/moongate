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
        var budget = new InstructionBudget(
            _state,
            10_000,
            10_000,
            100
        );
        budget.Install();
        var scheduler = new CoroutineScheduler(_state, _timers, budget, _ownership, _errors.Add, () => _owner);
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(_state, new TimerModule(_timers, scheduler, _ownership));
        Run("function wait(s) return coroutine.yield('wait', s) end");
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
    public void After_WithANonFunction_RaisesALuaError()
        => Assert.Throws<LuaRuntimeException>(() => Run("timer.after(1, 'nope')"));

    [Theory, InlineData("0"), InlineData("-1"), InlineData("0/0")]
    public void After_WithANonPositiveDuration_RaisesALuaErrorAndLeavesTheWheelAlone(string seconds)
    {
        var exception = Assert.Throws<LuaRuntimeException>(() => Run($"timer.after({seconds}, function() end)"));

        Assert.Contains("needs a positive number of seconds", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_timers.Timers);
    }

    [Fact]
    public void Cancel_AfterAOneShotTimerFired_ReportsFalse()
    {
        Run("h = timer.after(1, function() fired = true end)");
        _timers.Fire(Assert.Single(_timers.Timers).Id);

        Run("late = timer.cancel(h)");

        Assert.True(_state.Environment["fired"].Read<bool>());
        Assert.False(_state.Environment["late"].Read<bool>());
        Assert.Empty(_timers.Timers);
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
    public void Every_WhenTheCallbackThrows_ReportsEachFailure_AndTheTimerStaysAlive()
    {
        Run("timer.every(1, function() ticks = (ticks or 0) + 1 error('boom') end)");
        var timer = Assert.Single(_timers.Timers);

        _timers.Fire(timer.Id);
        _timers.Fire(timer.Id);

        Assert.Equal(2, _state.Environment["ticks"].Read<double>());
        Assert.Equal(2, _errors.Count);
        Assert.All(_errors, error => Assert.Contains("boom", error.Message, StringComparison.Ordinal));
        Assert.Single(_timers.Timers);
    }

    [Fact]
    public void Timers_AreOwnedByTheFileThatCreatedThem()
    {
        Run("timer.after(1, function() end)");

        Assert.Equal([_timers.Timers.Single().Id], _ownership.ReleaseTimers("init.lua"));
    }

    private LuaValue[] Run(string source)
        => SyncValueTask.Run(_state.DoStringAsync(source, "test"));

    public void Dispose()
        => _state.Dispose();
}
