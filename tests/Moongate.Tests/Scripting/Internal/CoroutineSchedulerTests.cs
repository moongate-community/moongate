using Lua;
using Lua.Standard;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Internal;
using Moongate.Scripting.Types.Scripts;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Internal;

public sealed class CoroutineSchedulerTests : IDisposable
{
    private readonly LuaState _state = LuaState.Create();
    private readonly RecordingTimerService _timers = new();
    private readonly List<ScriptErrorInfo> _errors = [];
    private readonly InstructionBudget _budget;
    private readonly ScriptOwnership _ownership = new();
    private readonly CoroutineScheduler _scheduler;

    public CoroutineSchedulerTests()
    {
        _state.OpenBasicLibrary();
        _state.OpenCoroutineLibrary();
        _state.OpenMathLibrary();
        _budget = new InstructionBudget(_state, maxInstructionsPerResume: 5_000, maxInstructionsPerChunk: 5_000, hookInterval: 100);
        _budget.Install();
        _scheduler = new CoroutineScheduler(_state, _timers, _budget, _ownership, _errors.Add, () => "test.lua");
        SyncValueTask.Run(_state.DoStringAsync("function wait(s) return coroutine.yield('wait', s) end", "prelude", default));
    }

    private LuaFunction Define(string name, string body)
    {
        SyncValueTask.Run(_state.DoStringAsync($"function {name}() {body} end", name + ".lua", default));

        return _state.Environment[name].Read<LuaFunction>();
    }

    [Fact]
    public void Start_FunctionThatReturns_CompletesWithItsValues()
    {
        var outcome = _scheduler.Start(Define("f", "return 1, 'two'"), "a.lua");

        Assert.Equal(ScriptResultKind.Completed, outcome.Kind);
        Assert.Equal([1d, "two"], outcome.Values);
        Assert.Equal(0, _scheduler.ActiveCount);
        Assert.Equal(1, _scheduler.Finished);
    }

    [Fact]
    public void Start_PassesArgumentsThrough()
    {
        SyncValueTask.Run(_state.DoStringAsync("function add(a, b) return a + b end", "add.lua", default));

        var outcome = _scheduler.Start(_state.Environment["add"].Read<LuaFunction>(), "a.lua", 2, 3);

        Assert.Equal([5d], outcome.Values);
    }

    [Fact]
    public void Start_Wait_SuspendsAndRegistersAOneShotTimerOfThatDuration()
    {
        var outcome = _scheduler.Start(Define("f", "wait(2.5) done = true"), "a.lua");

        Assert.Equal(ScriptResultKind.Suspended, outcome.Kind);
        Assert.Equal(1, _scheduler.ActiveCount);
        var timer = Assert.Single(_timers.Timers);
        Assert.Equal(TimeSpan.FromSeconds(2.5), timer.Interval);
        Assert.False(timer.Repeat);
        Assert.Equal(LuaValueType.Nil, _state.Environment["done"].Type);
    }

    [Fact]
    public void TimerCallback_ResumesTheCoroutineToCompletion()
    {
        _scheduler.Start(Define("f", "wait(1) done = true"), "a.lua");

        _timers.Fire(_timers.Timers.Single().Id);

        Assert.True(_state.Environment["done"].Read<bool>());
        Assert.Equal(0, _scheduler.ActiveCount);
        Assert.Equal(2, _scheduler.Resumed);
    }

    [Fact]
    public void Wait_ReturnsTheSecondsActuallyRequested()
    {
        _scheduler.Start(Define("f", "slept = wait(3)"), "a.lua");

        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Equal(3, _state.Environment["slept"].Read<double>());
    }

    [Fact]
    public void Start_FunctionThatErrors_ReportsFileLineAndMessage_AndKeepsOthersRunning()
    {
        _scheduler.Start(Define("ok", "wait(1)"), "ok.lua");

        var outcome = _scheduler.Start(Define("bad", "error('kaboom')"), "bad.lua");

        Assert.Equal(ScriptResultKind.Failed, outcome.Kind);
        var error = Assert.Single(_errors);
        Assert.Equal("bad.lua", error.File);
        Assert.Equal(1, error.Line);
        Assert.Contains("kaboom", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, _scheduler.ActiveCount);
        Assert.Equal(1, _scheduler.Errors);
    }

    [Fact]
    public void Start_RunawayLoop_IsAbortedByTheInstructionBudget()
    {
        var outcome = _scheduler.Start(Define("spin", "while true do end"), "spin.lua");

        Assert.Equal(ScriptResultKind.Failed, outcome.Kind);
        Assert.Contains("budget exceeded", outcome.Error!.Message, StringComparison.Ordinal);
        // The abort is built inside the hook, so it carries the position of the instruction that tripped it.
        Assert.Equal("spin.lua", outcome.Error.File);
        Assert.Equal(1, outcome.Error.Line);
        Assert.Equal(1, _scheduler.BudgetAborts);
        Assert.Equal(0, _scheduler.ActiveCount);
    }

    [Fact]
    public void Budget_CountsPerResume_NotPerCoroutineLifetime()
    {
        var outcome = _scheduler.Start(Define("f", "for i = 1, 3 do local n = 0 for j = 1, 1000 do n = n + j end wait(1) end finished = true"), "a.lua");
        Assert.Equal(ScriptResultKind.Suspended, outcome.Kind);

        _timers.Fire(_timers.Timers.Single().Id);
        _timers.Fire(_timers.Timers.Single().Id);
        _timers.Fire(_timers.Timers.Single().Id);

        Assert.True(_state.Environment["finished"].Read<bool>());
        Assert.Equal(0, _scheduler.BudgetAborts);
    }

    [Fact]
    public void Start_UnsupportedYield_FailsTheCoroutine()
    {
        var outcome = _scheduler.Start(Define("f", "coroutine.yield('something', 1)"), "a.lua");

        Assert.Equal(ScriptResultKind.Failed, outcome.Kind);
        Assert.Contains("unsupported yield", outcome.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(_timers.Timers);
    }

    [Theory, InlineData("0"), InlineData("-1"), InlineData("0/0"), InlineData("math.huge"), InlineData("1e300")]
    public void Wait_WithAnInvalidDuration_FailsTheCoroutineAndLeavesTheTimerServiceAlone(string seconds)
    {
        var outcome = _scheduler.Start(Define("f", $"wait({seconds}) reached = true"), "a.lua");

        Assert.Equal(ScriptResultKind.Failed, outcome.Kind);
        Assert.Contains("wait", outcome.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(_timers.Timers);
        Assert.Equal(0, _scheduler.ActiveCount);
        Assert.Equal(LuaValueType.Nil, _state.Environment["reached"].Type);
    }

    [Fact]
    public void Wait_InvalidDuration_OnATimerDrivenResume_DoesNotThrowOutOfTheCallback()
    {
        _scheduler.Start(Define("f", "wait(1) wait(0) reached = true"), "a.lua");

        _timers.Fire(_timers.Timers.Single().Id);

        Assert.Empty(_timers.Timers);
        Assert.Equal(0, _scheduler.ActiveCount);
        Assert.Single(_errors);
    }

    [Fact]
    public void Start_UnconvertibleArgument_ThrowsWithoutRegisteringACoroutine()
    {
        Assert.Throws<InvalidCastException>(() => _scheduler.Start(Define("f", "return 1"), "a.lua", new object()));

        Assert.Equal(0, _scheduler.ActiveCount);
        Assert.Empty(_ownership.ReleaseCoroutines("a.lua"));
    }

    [Fact]
    public void Start_FromInsideARunningResume_IsRefused()
    {
        InvalidOperationException? nested = null;
        _state.Environment["spawn"] = new LuaFunction("spawn", (context, _) =>
        {
            try
            {
                _scheduler.Start(_state.Environment["inner"].Read<LuaFunction>(), "a.lua");
            }
            catch (InvalidOperationException exception)
            {
                nested = exception;
            }

            return new ValueTask<int>(context.Return());
        });
        Define("inner", "return 1");

        _scheduler.Start(Define("outer", "spawn()"), "a.lua");

        Assert.NotNull(nested);
        Assert.Contains("nest", nested!.Message, StringComparison.Ordinal);
        Assert.Equal(0, _scheduler.ActiveCount);
    }

    [Fact]
    public void Budget_IsScopedPerUnit_SoATopLevelChunkAfterAResumeStartsFresh()
    {
        // Both units are a 2,000-iteration loop, about 4,000 instructions each (measured at ~2 per
        // iteration) against a 5,000 limit: each fits on its own, and their sum does not. A count that
        // leaked from the resume into the chunk would abort the chunk.
        _scheduler.Start(Define("f", "local n = 0 for i = 1, 2000 do n = n + i end wait(1)"), "a.lua");

        var result = _budget.Chunk(token =>
            SyncValueTask.Run(_state.DoStringAsync("local n = 0 for i = 1, 2000 do n = n + i end return n", "chunk", token)));

        Assert.Equal(2001000, result[0].Read<double>());
        Assert.Equal(0, _scheduler.BudgetAborts);
    }

    [Fact]
    public void Budget_SurvivesAnAbort_OnTheSameState()
    {
        var first = _scheduler.Start(Define("spin1", "while true do end"), "spin.lua");
        var second = _scheduler.Start(Define("spin2", "while true do end"), "spin.lua");

        Assert.Equal(ScriptResultKind.Failed, first.Kind);
        Assert.Contains("script budget exceeded", first.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(ScriptResultKind.Failed, second.Kind);
        Assert.Contains("script budget exceeded", second.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(2, _scheduler.BudgetAborts);
        Assert.Equal(0, _scheduler.ActiveCount);
    }

    [Fact]
    public void Budget_CannotBeSwallowedByPcall()
    {
        var outcome = _scheduler.Start(Define("f", "pcall(function() while true do end end) escaped = true"), "a.lua");

        Assert.Equal(ScriptResultKind.Failed, outcome.Kind);
        Assert.Contains("script budget exceeded", outcome.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(1, _scheduler.BudgetAborts);
        Assert.Equal(LuaValueType.Nil, _state.Environment["escaped"].Type);
    }

    [Fact]
    public void CancelOwned_DropsTheOwnersPendingTimersAndCoroutines_AndLeavesOthers()
    {
        _scheduler.Start(Define("a", "wait(1) a_done = true"), "a.lua");
        _scheduler.Start(Define("b", "wait(1) b_done = true"), "b.lua");

        _scheduler.CancelOwned("a.lua");

        Assert.Equal(1, _scheduler.ActiveCount);
        var remaining = Assert.Single(_timers.Timers);
        _timers.Fire(remaining.Id);
        Assert.True(_state.Environment["b_done"].Read<bool>());
        Assert.Equal(LuaValueType.Nil, _state.Environment["a_done"].Type);
    }

    public void Dispose()
    {
        _state.Dispose();
    }
}
