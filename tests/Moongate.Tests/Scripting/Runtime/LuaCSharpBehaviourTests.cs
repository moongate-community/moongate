using Lua;
using Lua.Runtime;
using Lua.Standard;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Runtime;

/// <summary>
/// Pins the LuaCSharp behaviours the scripting design depends on. A failure here means the runtime
/// changed under us; fix the engine before fixing the test.
/// </summary>
public sealed class LuaCSharpBehaviourTests
{
    private static T Sync<T>(ValueTask<T> task)
    {
        Assert.True(task.IsCompleted, "LuaCSharp completed asynchronously.");

        return task.GetAwaiter().GetResult();
    }

    [Fact]
    public void SelectiveLibraries_LeaveIoAndOsUndefined()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenMathLibrary();

        var result = Sync(state.DoStringAsync("return tostring(io), tostring(os), string.upper('ok')", "probe", default));

        Assert.Equal("nil", result[0].Read<string>());
        Assert.Equal("nil", result[1].Read<string>());
        Assert.Equal("OK", result[2].Read<string>());
    }

    [Fact]
    public void Coroutine_YieldCompletesSynchronouslyAndLeavesValuesOnTheStack()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenCoroutineLibrary();
        Sync(state.DoStringAsync(
            "function job() local got = coroutine.yield('wait', 2) return 'done:' .. tostring(got) end", "probe", default));
        var coroutine = state.CreateCoroutine(state.Environment["job"].Read<LuaFunction>(), isProtectedMode: true);
        var stack = new LuaStack(16);

        var first = Sync(coroutine.ResumeAsync(stack, default));

        Assert.Equal(3, first);
        Assert.Equal(LuaThreadStatus.Suspended, coroutine.GetStatus());
        Assert.True(stack.AsSpan()[0].Read<bool>());
        Assert.Equal("wait", stack.AsSpan()[1].Read<string>());
        Assert.Equal(2, stack.AsSpan()[2].Read<double>());

        stack.Clear();
        stack.Push(new LuaValue("resumed"));
        var second = Sync(coroutine.ResumeAsync(stack, default));

        Assert.Equal(2, second);
        Assert.Equal(LuaThreadStatus.Dead, coroutine.GetStatus());
        Assert.Equal("done:resumed", stack.AsSpan()[1].Read<string>());
    }

    [Fact]
    public void ProtectedCoroutine_ReportsAnErrorAsAValueInsteadOfThrowing()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenCoroutineLibrary();
        Sync(state.DoStringAsync("function boom() error('kaboom') end", "scripts/boom.lua", default));
        var coroutine = state.CreateCoroutine(state.Environment["boom"].Read<LuaFunction>(), isProtectedMode: true);
        var stack = new LuaStack(8);

        var count = Sync(coroutine.ResumeAsync(stack, default));

        Assert.Equal(2, count);
        Assert.Equal(LuaThreadStatus.Dead, coroutine.GetStatus());
        Assert.False(stack.AsSpan()[0].Read<bool>());
        Assert.Contains("kaboom", stack.AsSpan()[1].Read<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnprotectedCoroutine_ThrowsWithTheChunkNameAndLine()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenCoroutineLibrary();
        Sync(state.DoStringAsync("function boom() error('kaboom') end", "scripts/boom.lua", default));
        var coroutine = state.CreateCoroutine(state.Environment["boom"].Read<LuaFunction>(), isProtectedMode: false);

        var exception = Assert.Throws<LuaRuntimeException>(() => Sync(coroutine.ResumeAsync(new LuaStack(8), default)));

        Assert.Contains("[string \"scripts/boom.lua\"]:1:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("kaboom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountHook_InterruptsABareInfiniteLoop()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var hits = 0;
        state.SetHook(
            new LuaFunction("budget", (context, _) =>
            {
                hits++;

                if (hits >= 3)
                {
                    throw new LuaRuntimeException(context.State, new LuaValue("budget exceeded"), 1);
                }

                return new ValueTask<int>(context.Return());
            }),
            "",
            1000
        );

        var exception = Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("while true do end", "probe", default)));

        Assert.Contains("budget exceeded", exception.Message, StringComparison.Ordinal);
        Assert.Equal(3, hits);
    }

    [Fact]
    public void AHookThatThrows_StopsFiringForTheRestOfTheStatesLife()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var hits = 0;
        void InstallThrowingHook()
        {
            state.SetHook(
                new LuaFunction("budget", (context, _) =>
                {
                    hits++;

                    if (hits % 3 == 0)
                    {
                        throw new LuaRuntimeException(context.State, new LuaValue("budget exceeded"), 1);
                    }

                    return new ValueTask<int>(context.Return());
                }),
                "",
                1000
            );
        }

        InstallThrowingHook();

        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("while true do end", "first", default)));
        Assert.Equal(3, hits);

        // The VM leaves its in-hook flag set when the hook throws, so the hook never fires again on this
        // state. The next chunk is a bounded ~10,000-instruction loop, which a live hook would count ten
        // times over; re-installing the hook does not revive it either.
        Sync(state.DoStringAsync("local n = 0 for i = 1, 5000 do n = n + i end return n", "second", default));

        Assert.Equal(3, hits);

        InstallThrowingHook();
        Sync(state.DoStringAsync("local n = 0 for i = 1, 5000 do n = n + i end return n", "third", default));

        Assert.Equal(3, hits);
    }

    [Fact]
    public void Pcall_SwallowsAnExceptionThrownFromAHook()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var hits = 0;
        state.SetHook(
            new LuaFunction("budget", (context, _) =>
            {
                hits++;

                if (hits >= 3)
                {
                    throw new LuaRuntimeException(context.State, new LuaValue("budget exceeded"), 1);
                }

                return new ValueTask<int>(context.Return());
            }),
            "",
            1000
        );

        var result = Sync(state.DoStringAsync(
            "local ok, err = pcall(function() while true do end end) return ok, tostring(err)", "probe", default));

        Assert.False(result[0].Read<bool>());
        Assert.Contains("budget exceeded", result[1].Read<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACancelledTokenFromInsideTheHook_InterruptsABareInfiniteLoop()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        using var source = new CancellationTokenSource();
        var instructions = 0;
        state.SetHook(
            new LuaFunction("budget", (context, _) =>
            {
                instructions += 1000;

                if (instructions > 5000)
                {
                    source.Cancel();
                }

                return new ValueTask<int>(context.Return());
            }),
            "",
            1000
        );
        var closure = state.Load("while true do end".AsSpan(), "probe", state.Environment);

        Assert.Throws<LuaCanceledException>(() => Sync(state.ExecuteAsync(closure, source.Token)));

        Assert.Equal(6000, instructions);
    }

    [Fact]
    public void ACancelledTokenIsNotSwallowedByPcall()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        using var source = new CancellationTokenSource();
        var instructions = 0;
        state.SetHook(
            new LuaFunction("budget", (context, _) =>
            {
                instructions += 1000;

                if (instructions > 5000)
                {
                    source.Cancel();
                }

                return new ValueTask<int>(context.Return());
            }),
            "",
            1000
        );
        var closure = state.Load(
            "local ok = pcall(function() while true do end end) escaped = true return ok".AsSpan(), "probe", state.Environment);

        Assert.Throws<LuaCanceledException>(() => Sync(state.ExecuteAsync(closure, source.Token)));

        Assert.Equal(LuaValueType.Nil, state.Environment["escaped"].Type);
    }

    [Fact]
    public void LoadedModules_EvictionMakesRequireReloadTheModule()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();
        var loads = 0;
        state.ModuleLoader = new CountingModuleLoader(() => { loads++; return $"return {{ n = {loads} }}"; });

        var first = Sync(state.DoStringAsync("return require('m').n", "probe", default))[0].Read<double>();
        var cached = Sync(state.DoStringAsync("return require('m').n", "probe", default))[0].Read<double>();
        state.LoadedModules["m"] = LuaValue.Nil;
        var reloaded = Sync(state.DoStringAsync("return require('m').n", "probe", default))[0].Read<double>();

        Assert.Equal(1, first);
        Assert.Equal(1, cached);
        Assert.Equal(2, reloaded);
    }

    [Fact]
    public void RuntimeError_CarriesTheChunkNameAndLine()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var closure = state.Load("local a = nil\nreturn a.b".AsSpan(), "scripts/ai/x.lua", state.Environment);

        var exception = Assert.Throws<LuaRuntimeException>(() => Sync(state.ExecuteAsync(closure, default)));

        Assert.Contains("[string \"scripts/ai/x.lua\"]:2:", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, exception.LuaTraceback.FirstLine);
    }

    [Fact]
    public void ReadOnlyProxy_BlocksWritesToExistingAndNewKeys()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        var hidden = new LuaTable();
        hidden["LIMIT"] = new LuaValue(42);
        var meta = new LuaTable();
        meta["__index"] = new LuaValue(hidden);
        meta["__newindex"] = new LuaFunction("ro", (context, _) =>
            throw new LuaRuntimeException(context.State, new LuaValue("read-only"), 1));
        meta["__metatable"] = new LuaValue("locked");
        var proxy = new LuaTable { Metatable = meta };
        state.Environment["m"] = proxy;

        Assert.Equal(42, Sync(state.DoStringAsync("return m.LIMIT", "p", default))[0].Read<double>());
        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("m.LIMIT = 1", "p", default)));
        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("m.NEW = 1", "p", default)));
        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("setmetatable(m, {})", "p", default)));
    }
}
