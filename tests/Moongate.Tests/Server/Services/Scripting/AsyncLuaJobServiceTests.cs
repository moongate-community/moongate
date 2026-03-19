using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Services.Scripting.Jobs;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class AsyncLuaJobServiceTests
{
    private sealed class TestHandler : IAsyncLuaJobHandler
    {
        public required string Name { get; init; }

        public Func<IReadOnlyDictionary<string, object?>, CancellationToken, Task<Dictionary<string, object?>>> Callback { get; init; }
            = (_, _) => Task.FromResult(new Dictionary<string, object?>());

        public Task<Dictionary<string, object?>> ExecuteAsync(
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken
        )
            => Callback(payload, cancellationToken);
    }

    [Test]
    public void TryConvertPayload_ShouldConvertPrimitiveAndNestedValues()
    {
        var converter = new AsyncLuaValueConverter();
        var script = new Script();
        var payload = new Table(script);
        var nested = new Table(script);
        var items = new Table(script);

        nested["name"] = "tommy";
        items[1] = "a";
        items[2] = 2;
        payload["flag"] = true;
        payload["count"] = 3;
        payload["nested"] = nested;
        payload["items"] = items;

        var converted = converter.TryConvertPayload(payload, out var result);

        Assert.Multiple(
            () =>
            {
                Assert.That(converted, Is.True);
                Assert.That(result["flag"], Is.EqualTo(true));
                Assert.That(result["count"], Is.EqualTo(3d));
                Assert.That(result["nested"], Is.TypeOf<Dictionary<string, object?>>());
                Assert.That(result["items"], Is.TypeOf<List<object?>>());
            }
        );
    }

    [Test]
    public void TryConvertPayload_WhenUnsupportedLuaValueIsUsed_ShouldReturnFalse()
    {
        var converter = new AsyncLuaValueConverter();
        var script = new Script();
        var payload = new Table(script);

        payload["bad"] = DynValue.NewCallback((_, _) => DynValue.Nil);

        var converted = converter.TryConvertPayload(payload, out var result);

        Assert.Multiple(
            () =>
            {
                Assert.That(converted, Is.False);
                Assert.That(result, Is.Empty);
            }
        );
    }

    [Test]
    public async Task Run_ShouldScheduleHandlerAndInvokeResultCallbackOnGameLoop()
    {
        using var backgroundJobs = new BackgroundJobService();
        backgroundJobs.Start(1);
        var scheduler = new AsyncWorkSchedulerService(backgroundJobs);
        var registry = new AsyncLuaJobRegistry();
        var handler = new TestHandler
        {
            Name = "echo",
            Callback = (payload, _) => Task.FromResult(
                new Dictionary<string, object?>
                {
                    ["payload"] = new Dictionary<string, object?>(payload, StringComparer.Ordinal)
                }
            )
        };
        _ = registry.TryRegister(handler);
        var scriptEngine = CreateScriptEngine();
        var service = new AsyncLuaJobService(scheduler, registry, scriptEngine, new AsyncLuaValueConverter());
        scriptEngine.ExecuteScript(@"
captured_result = nil
captured_error = nil
function on_async_job_result(job_name, request_id, result)
  captured_result = { job_name = job_name, request_id = request_id, payload = result.payload }
end
function on_async_job_error(job_name, request_id, message)
  captured_error = { job_name = job_name, request_id = request_id, message = message }
end
");
        var payload = new Table(scriptEngine.LuaScript);
        payload["text"] = "hello";

        var scheduled = service.Run("echo", "req-1", payload);
        var callbackQueued = await WaitUntilAsync(
                                 () => backgroundJobs.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        var result = scriptEngine.ExecuteFunction("captured_result");
        var error = scriptEngine.ExecuteFunction("captured_error");
        await backgroundJobs.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(scheduled, Is.True);
                Assert.That(callbackQueued, Is.True);
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.Not.Null);
                Assert.That(error.Data, Is.Null);
            }
        );
    }

    [Test]
    public async Task TryRun_WhenKeyIsAlreadyInFlight_ShouldRejectDuplicate()
    {
        using var backgroundJobs = new BackgroundJobService();
        backgroundJobs.Start(1);
        var scheduler = new AsyncWorkSchedulerService(backgroundJobs);
        var registry = new AsyncLuaJobRegistry();
        var gate = new TaskCompletionSource<Dictionary<string, object?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = registry.TryRegister(
            new TestHandler
            {
                Name = "slow",
                Callback = (_, _) => gate.Task
            }
        );
        var scriptEngine = CreateScriptEngine();
        var service = new AsyncLuaJobService(scheduler, registry, scriptEngine, new AsyncLuaValueConverter());

        var first = service.TryRun("slow", "npc:1", "req-1");
        var second = service.TryRun("slow", "npc:1", "req-2");

        gate.SetResult(new Dictionary<string, object?>());
        _ = await WaitUntilAsync(() => backgroundJobs.ExecutePendingOnGameLoop() > 0, TimeSpan.FromSeconds(2));
        var third = service.TryRun("slow", "npc:1", "req-3");
        await backgroundJobs.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
                Assert.That(third, Is.True);
            }
        );
    }

    [Test]
    public async Task Run_WhenHandlerFails_ShouldInvokeErrorCallback()
    {
        using var backgroundJobs = new BackgroundJobService();
        backgroundJobs.Start(1);
        var scheduler = new AsyncWorkSchedulerService(backgroundJobs);
        var registry = new AsyncLuaJobRegistry();
        _ = registry.TryRegister(
            new TestHandler
            {
                Name = "boom",
                Callback = (_, _) => throw new InvalidOperationException("boom")
            }
        );
        var scriptEngine = CreateScriptEngine();
        var service = new AsyncLuaJobService(scheduler, registry, scriptEngine, new AsyncLuaValueConverter());
        scriptEngine.ExecuteScript(@"
captured_error = nil
function on_async_job_error(job_name, request_id, message)
  captured_error = { job_name = job_name, request_id = request_id, message = message }
end
");

        var scheduled = service.Run("boom", "req-err");
        var callbackQueued = await WaitUntilAsync(
                                 () => backgroundJobs.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        var error = scriptEngine.ExecuteFunction("captured_error");
        await backgroundJobs.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(scheduled, Is.True);
                Assert.That(callbackQueued, Is.True);
                Assert.That(error.Success, Is.True);
                Assert.That(error.Data, Is.Not.Null);
            }
        );
    }

    [Test]
    public void Run_WhenJobIsUnknown_ShouldReturnFalse()
    {
        var service = new AsyncLuaJobService(
            new AsyncWorkSchedulerService(new BackgroundJobService()),
            new AsyncLuaJobRegistry(),
            CreateScriptEngine(),
            new AsyncLuaValueConverter()
        );

        var scheduled = service.Run("missing", "req-1");

        Assert.That(scheduled, Is.False);
    }

    [Test]
    public async Task EchoAsyncLuaJobHandler_ShouldWrapPayloadUnderPayloadField()
    {
        IAsyncLuaJobHandler handler = new EchoAsyncLuaJobHandler();
        var result = await handler.ExecuteAsync(
                         new Dictionary<string, object?>
                         {
                             ["text"] = "hello"
                         },
                         CancellationToken.None
                     );

        Assert.That(result["payload"], Is.TypeOf<Dictionary<string, object?>>());
    }

    private static LuaScriptEngineService CreateScriptEngine()
    {
        var root = Path.GetTempPath();
        var dirs = new DirectoriesConfig(root, Enum.GetNames<DirectoryType>());
        Directory.CreateDirectory(dirs[DirectoryType.Scripts]);
        Directory.CreateDirectory(root);
        return new LuaScriptEngineService(
            dirs,
            new List<ScriptModuleData>(),
            new DryIoc.Container(),
            new LuaEngineConfig(root, dirs[DirectoryType.Scripts], "0.1.0"),
            []
        );
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20);
        }

        return condition();
    }
}
