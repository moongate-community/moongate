using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Scripting.Modules;
using Moongate.Server.Services.Game;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Scripting.Lua.Extensions.Scripts;
using SquidStd.Scripting.Lua.Interfaces.Scripts;
using SquidStd.Services.Core.Extensions;
using SquidStd.Services.Core.Services.Bootstrap;

namespace Moongate.Tests.Scripting;

public class GameLoopModuleTests
{
    [Fact]
    public async Task GameModule_FromLua_PostsToLoopAndSchedules()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-lua-" + Guid.NewGuid().ToString("N"));
        var scripts = Path.Combine(root, "scripts");
        Directory.CreateDirectory(scripts);

        var bootstrap = SquidStdBootstrap.Create(new SquidStdOptions { ConfigName = "moongate", RootDirectory = root });

        bootstrap.ConfigureServices(container =>
            {
                // RegisterCoreServices already provides IMainThreadDispatcher and ITimerService.
                container.RegisterCoreServices();
                container.Register<IGameLoopContext, GameLoopContext>(Reuse.Singleton);
                container.RegisterLuaEngine(new(root, scripts, "MoongateTests", "1.0.0"));
                container.RegisterScriptModule<GameLoopModule>();

                return container;
            }
        );

        await bootstrap.StartAsync();

        try
        {
            var engine = bootstrap.Resolve<IScriptEngineService>();
            var dispatcher = bootstrap.Resolve<IMainThreadDispatcher>();

            // The module is exposed to Lua as the global `game`.
            Assert.Equal("table", engine.ExecuteFunction("type(game)").Data);

            // game.post defers the callback to the loop thread: the global must not flip until the
            // dispatcher drains. Observed through a shared Lua global (same script state).
            engine.ExecuteScript("ran = false");
            engine.ExecuteScript("game.post(function() ran = true end)");
            Assert.True(engine.ExecuteFunction("ran").Data is false);

            dispatcher.DrainPending();
            Assert.True(engine.ExecuteFunction("ran").Data is true);

            // game.schedule returns a timer id that game.cancel can remove (long delay so it never fires here).
            var timerId = engine.ExecuteFunction("game.schedule('probe', 60000, function() end)").Data as string;
            Assert.False(string.IsNullOrEmpty(timerId));

            var cancelled = engine.ExecuteFunction($"game.cancel('{timerId}')").Data;
            Assert.True(cancelled is true);
        }
        finally
        {
            await bootstrap.StopAsync();
            Directory.Delete(root, true);
        }
    }
}
