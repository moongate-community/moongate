using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Scripting.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Integration.Scripting;

public sealed class ScriptingBootstrapTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Bootstrap_RunsInitLuaOnTheLoop_FiresATimerThroughTheWheel_AndWritesDefinitions()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write(
            "init.lua",
            "log.info('booted {Engine}', engine.name)\n" +
            "timer.after(0.05, function() wait(0.05) fired = (fired or 0) + 1 end)\n" +
            "function fired_count() return fired or 0 end"
        );
        using var container = new Container();
        container.RegisterMoongateEventBus();
        container.RegisterInstance<TimeProvider>(TimeProvider.System);
        container.RegisterInstance(new TimerWheelOptions());
        container.RegisterInstance(new GameLoopOptions());
        container.RegisterInstance(new ScriptEngineOptions { ScriptsDirectory = scripts.Path });
        container.RegisterDelegate<ITimerService>(resolver => resolver.Resolve<TimerWheelService>(), Reuse.Singleton);
        container.AddMoongateService<TimerWheelService>(-900)
            .AddMoongateService<IGameLoopService, GameLoopService>(-800)
            .AddMoongateService<IEventBusService, EventBusService>()
            .AddMoongateService<IScriptEngine, LuaScriptEngineService>(LuaScriptEngineService.StartupPriority)
            .AddScriptModule<LogModule>();
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync().WaitAsync(Timeout);

        try
        {
            var engine = container.Resolve<IScriptEngine>();
            var loop = container.Resolve<IGameLoopService>();
            Assert.Equal(1, engine.GetMetrics().FilesLoaded);
            Assert.True(File.Exists(Path.Combine(scripts.Path, "definitions.lua")));
            Assert.True(File.Exists(Path.Combine(scripts.Path, ".luarc.json")));

            var deadline = DateTime.UtcNow + Timeout;
            var count = 0d;

            while (count < 1 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
                var probe = new TaskCompletionSource<double>(TaskCreationOptions.RunContinuationsAsynchronously);
                await loop.PostAsync(
                    new ActionGameLoopWorkItem(() =>
                        probe.SetResult((double)engine.Call("fired_count").Values[0]!)
                    )
                );
                count = await probe.Task.WaitAsync(Timeout);
            }

            Assert.Equal(1, count);
            Assert.Throws<InvalidOperationException>(() => engine.Call("fired_count"));
        }
        finally
        {
            await bootstrap.StopAsync().WaitAsync(Timeout);
        }
    }
}
