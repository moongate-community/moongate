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
        Sync(
            state.DoStringAsync(
                "function job() local got = coroutine.yield('wait', 2) return 'done:' .. tostring(got) end",
                "probe",
                default
            )
        );
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
            new LuaFunction(
                "budget",
                (context, _) =>
                {
                    hits++;

                    if (hits >= 3)
                    {
                        throw new LuaRuntimeException(context.State, new LuaValue("budget exceeded"), 1);
                    }

                    return new ValueTask<int>(context.Return());
                }
            ),
            "",
            1000
        );

        var exception =
            Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("while true do end", "probe", default)));

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
                new LuaFunction(
                    "budget",
                    (context, _) =>
                    {
                        hits++;

                        if (hits % 3 == 0)
                        {
                            throw new LuaRuntimeException(context.State, new LuaValue("budget exceeded"), 1);
                        }

                        return new ValueTask<int>(context.Return());
                    }
                ),
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
            new LuaFunction(
                "budget",
                (context, _) =>
                {
                    hits++;

                    if (hits >= 3)
                    {
                        throw new LuaRuntimeException(context.State, new LuaValue("budget exceeded"), 1);
                    }

                    return new ValueTask<int>(context.Return());
                }
            ),
            "",
            1000
        );

        var result = Sync(
            state.DoStringAsync(
                "local ok, err = pcall(function() while true do end end) return ok, tostring(err)",
                "probe",
                default
            )
        );

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
            new LuaFunction(
                "budget",
                (context, _) =>
                {
                    instructions += 1000;

                    if (instructions > 5000)
                    {
                        source.Cancel();
                    }

                    return new ValueTask<int>(context.Return());
                }
            ),
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
            new LuaFunction(
                "budget",
                (context, _) =>
                {
                    instructions += 1000;

                    if (instructions > 5000)
                    {
                        source.Cancel();
                    }

                    return new ValueTask<int>(context.Return());
                }
            ),
            "",
            1000
        );
        var closure = state.Load(
            "local ok = pcall(function() while true do end end) escaped = true return ok".AsSpan(),
            "probe",
            state.Environment
        );

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
        state.ModuleLoader = new CountingModuleLoader(() =>
            {
                loads++;
                return $"return {{ n = {loads} }}";
            }
        );

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
    public void OpenBasicLibrary_DefinesDofileAndLoadfile()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();

        var result = Sync(state.DoStringAsync("return type(dofile), type(loadfile), type(rawset)", "probe", default));

        Assert.Equal("function", result[0].Read<string>());
        Assert.Equal("function", result[1].Read<string>());
        Assert.Equal("function", result[2].Read<string>());
    }

    [Fact]
    public void OpenModuleLibrary_InstallsTwoSearchers_AndTheSecondHonoursPackagePath()
    {
        using var outside = new TemporaryScriptsDirectory();
        outside.Write("intruder.lua", "return 'loaded from package.path'");
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenStringLibrary();
        state.OpenModuleLibrary();
        state.ModuleLoader = new CountingModuleLoader(() => "return {}");

        Assert.Equal(2, Sync(state.DoStringAsync("return #package.searchers", "probe", default))[0].Read<double>());

        var result = Sync(
            state.DoStringAsync(
                $"package.path = [[{outside.Path.Replace('\\', '/')}/?.lua]] return require('intruder')",
                "probe",
                default
            )
        );

        Assert.Equal("loaded from package.path", result[0].Read<string>());
    }

    [Fact]
    public void ASuspendedCoroutine_KeepsTheTokenOfItsFirstResume()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenCoroutineLibrary();
        Sync(
            state.DoStringAsync(
                "function job() coroutine.yield('wait', 1) local n = 0 for i = 1, 5000 do n = n + i end return n end",
                "probe",
                default
            )
        );
        var job = state.Environment["job"].Read<LuaFunction>();
        CancellationTokenSource? target = null;
        var instructions = 0;
        var hook = new LuaFunction(
            "budget",
            (context, _) =>
            {
                instructions += 500;

                if (instructions > 1000)
                {
                    target?.Cancel();
                }

                return new ValueTask<int>(context.Return());
            }
        );

        // Cancelling the token of the *second* resume does not stop the coroutine: the ~10,000-instruction
        // loop runs to its end even though the token it was resumed with is cancelled.
        using var firstResume = new CancellationTokenSource();
        using var secondResume = new CancellationTokenSource();
        var yielded = state.CreateCoroutine(job, isProtectedMode: false);
        yielded.SetHook(hook, "", 500);
        var stack = new LuaStack(16);
        Sync(yielded.ResumeAsync(stack, firstResume.Token));
        Assert.Equal(LuaThreadStatus.Suspended, yielded.GetStatus());
        target = secondResume;
        instructions = 0;
        stack.Clear();
        stack.Push(new LuaValue(1));

        var count = Sync(yielded.ResumeAsync(stack, secondResume.Token));

        Assert.Equal(2, count);
        Assert.Equal(LuaThreadStatus.Dead, yielded.GetStatus());
        Assert.Equal(12502500, stack.AsSpan()[1].Read<double>());
        Assert.True(secondResume.IsCancellationRequested);
        Assert.True(instructions > 1000, $"the hook fired {instructions} instructions in, so it did cancel");

        // Cancelling the token of the *first* resume does stop it, on the second resume.
        using var keptToken = new CancellationTokenSource();
        using var ignoredToken = new CancellationTokenSource();
        var other = state.CreateCoroutine(job, isProtectedMode: false);
        other.SetHook(hook, "", 500);
        var otherStack = new LuaStack(16);
        Sync(other.ResumeAsync(otherStack, keptToken.Token));
        target = keptToken;
        instructions = 0;
        otherStack.Clear();
        otherStack.Push(new LuaValue(1));

        Assert.Throws<LuaCanceledException>(() => Sync(other.ResumeAsync(otherStack, ignoredToken.Token)));

        Assert.False(ignoredToken.IsCancellationRequested);
    }

    [Fact]
    public void ACoroutineCreatedFromLua_DoesNotInheritTheHook()
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenCoroutineLibrary();
        var hits = 0;
        state.SetHook(
            new LuaFunction(
                "count",
                (context, _) =>
                {
                    hits++;

                    return new ValueTask<int>(context.Return());
                }
            ),
            "",
            1000
        );
        const string Loop = "local n = 0 for i = 1, 5000 do n = n + i end return n";

        Sync(state.DoStringAsync(Loop, "inline", default));
        var inlineHits = hits;
        hits = 0;
        Sync(state.DoStringAsync($"local f = coroutine.wrap(function() {Loop} end) return f()", "viaCoroutine", default));

        Assert.True(inlineHits >= 5, $"the ~10,000-instruction loop should fire the hook repeatedly, fired {inlineHits}");
        Assert.Equal(0, hits);
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
        meta["__newindex"] = new LuaFunction(
            "ro",
            (context, _) =>
                throw new LuaRuntimeException(context.State, new LuaValue("read-only"), 1)
        );
        meta["__metatable"] = new LuaValue("locked");
        var proxy = new LuaTable { Metatable = meta };
        state.Environment["m"] = proxy;

        Assert.Equal(42, Sync(state.DoStringAsync("return m.LIMIT", "p", default))[0].Read<double>());
        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("m.LIMIT = 1", "p", default)));
        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("m.NEW = 1", "p", default)));
        Assert.Throws<LuaRuntimeException>(() => Sync(state.DoStringAsync("setmetatable(m, {})", "p", default)));
    }
}
