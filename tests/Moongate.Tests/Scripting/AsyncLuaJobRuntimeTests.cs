using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Modules;
using Moongate.Server.Services.EventLoop;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Services.Scripting.Jobs;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Scripting;

public sealed class AsyncLuaJobRuntimeTests
{
    private sealed class ThrowingAsyncLuaJobHandler : IAsyncLuaJobHandler
    {
        public string Name => "boom";

        public Task<Dictionary<string, object?>> ExecuteAsync(
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken
        )
        {
            _ = payload;
            _ = cancellationToken;
            throw new InvalidOperationException("boom");
        }
    }

    [Test]
    public async Task StartAsync_WithAsyncJobModule_ShouldInvokeResultCallback()
    {
        using var backgroundJobs = new BackgroundJobService();
        backgroundJobs.Start(1);
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        Directory.CreateDirectory(dirs[DirectoryType.Scripts]);

        var container = new Container();
        var registry = new AsyncLuaJobRegistry();
        _ = registry.TryRegister(new EchoAsyncLuaJobHandler());

        var engine = new LuaScriptEngineService(
            dirs,
            [new(typeof(AsyncJobModule))],
            container,
            new LuaEngineConfig(temp.Path, dirs[DirectoryType.Scripts], "0.1.0"),
            []
        );
        IAsyncWorkSchedulerService scheduler = new AsyncWorkSchedulerService(backgroundJobs);
        IAsyncLuaJobService service = new AsyncLuaJobService(scheduler, registry, engine, new AsyncLuaValueConverter());
        container.RegisterInstance(service);

        await engine.StartAsync();
        engine.ExecuteScript(@"
captured = nil
function on_async_job_result(job_name, request_id, result)
  captured = { job_name = job_name, request_id = request_id, text = result.payload.text }
end
");

        var execute = engine.ExecuteFunction(@"
            (function()
                return async_job.run('echo', 'req-1', { text = 'hello' })
            end)()
        ");
        var callbackQueued = await WaitUntilAsync(
                                 () => backgroundJobs.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        var captured = engine.ExecuteFunction("captured");
        await backgroundJobs.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(execute.Success, Is.True);
                Assert.That(execute.Data, Is.EqualTo(true));
                Assert.That(callbackQueued, Is.True);
                Assert.That(captured.Success, Is.True);
                Assert.That(captured.Data, Is.Not.Null);
            }
        );
    }

    [Test]
    public async Task StartAsync_WithAsyncJobModule_ShouldInvokeErrorCallback()
    {
        using var backgroundJobs = new BackgroundJobService();
        backgroundJobs.Start(1);
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        Directory.CreateDirectory(dirs[DirectoryType.Scripts]);

        var container = new Container();
        var registry = new AsyncLuaJobRegistry();
        _ = registry.TryRegister(new ThrowingAsyncLuaJobHandler());

        var engine = new LuaScriptEngineService(
            dirs,
            [new(typeof(AsyncJobModule))],
            container,
            new LuaEngineConfig(temp.Path, dirs[DirectoryType.Scripts], "0.1.0"),
            []
        );
        IAsyncWorkSchedulerService scheduler = new AsyncWorkSchedulerService(backgroundJobs);
        IAsyncLuaJobService service = new AsyncLuaJobService(scheduler, registry, engine, new AsyncLuaValueConverter());
        container.RegisterInstance(service);

        await engine.StartAsync();
        engine.ExecuteScript(@"
captured_error = nil
function on_async_job_error(job_name, request_id, message)
  captured_error = { job_name = job_name, request_id = request_id, message = message }
end
");

        var execute = engine.ExecuteFunction(@"
            (function()
                return async_job.run('boom', 'req-err', { })
            end)()
        ");
        var callbackQueued = await WaitUntilAsync(
                                 () => backgroundJobs.ExecutePendingOnGameLoop() > 0,
                                 TimeSpan.FromSeconds(2)
                             );
        var captured = engine.ExecuteFunction("captured_error");
        await backgroundJobs.StopAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(execute.Success, Is.True);
                Assert.That(execute.Data, Is.EqualTo(true));
                Assert.That(callbackQueued, Is.True);
                Assert.That(captured.Success, Is.True);
                Assert.That(captured.Data, Is.Not.Null);
            }
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
