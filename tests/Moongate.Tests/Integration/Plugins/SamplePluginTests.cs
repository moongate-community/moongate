using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.GameLoop;
using Moongate.Server.Core.Data.Timing;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Diagnostics;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Plugins;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Support.GameLoop;
using Moongate.Tests.TestSupport.Diagnostics;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Plugins;

namespace Moongate.Tests.Integration.Plugins;

[Collection(PostgresTestCollection.Name)]
public sealed class SamplePluginTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SampleBundle_LoadsThroughTheLoader_AndItsModuleCommandAndMetricWork()
    {
        using var files = new PluginDirectoryFixture("plugins", "scripts");
        files.Deploy("SamplePlugin", "sample");
        await File.WriteAllTextAsync(
            Path.Combine(files.Directories["scripts"], "init.lua"),
            "greeting = greeter.hello('Moongate', Tone.Warm)\n" +
            "function report() return greeting, greeter.DEFAULT_GREETING end"
        );
        await using var persistence = await HostPersistenceFixture.CreateAsync();
        var container = persistence.Container;
        container.RegisterMoongateEventBus();
        container.RegisterInstance(TimeProvider.System);
        container.RegisterInstance(new TimerWheelOptions());
        container.RegisterInstance(new GameLoopOptions());
        container.RegisterInstance(files.Directories);
        container.RegisterInstance(new ScriptEngineOptions { ScriptsDirectory = files.Directories["scripts"] });
        container.RegisterDelegate<ITimerService>(resolver => resolver.Resolve<TimerWheelService>(), Reuse.Singleton);
        container.AddMoongateService<TimerWheelService>(-900)
            .AddMoongateService<IGameLoopService, GameLoopService>(-800)
            .AddMoongateService<IEventBusService, EventBusService>()
            .AddMoongateService<IPluginLoaderService, PluginLoaderService>(() =>
                new(container, files.Directories)
            )
            .AddMoongateService<IScriptEngine, LuaScriptEngineService>(LuaScriptEngineService.StartupPriority);
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync().WaitAsync(Timeout);
        Assert.True(await persistence.Database.ScalarAsync<bool>("SELECT to_regclass('sample_greeter.notes') IS NOT NULL"));

        try
        {
            var loader = container.Resolve<IPluginLoaderService>();
            Assert.Contains(loader.Plugins, plugin => plugin.Id == "com.github.moongate-community.moongate.plugins.greeter");

            var engine = container.Resolve<IScriptEngine>();
            var loop = container.Resolve<IGameLoopService>();
            var probe = new TaskCompletionSource<object?[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            await loop.PostAsync(
                new ActionGameLoopWorkItem(() =>
                    {
                        try
                        {
                            probe.SetResult(engine.Call("report").Values.ToArray());
                        }
                        catch (Exception exception)
                        {
                            probe.SetException(exception);
                        }
                    }
                )
            );
            Assert.Equal(["Hello there, Moongate!", "Hello"], await probe.Task.WaitAsync(Timeout));

            var commands = new CommandSystemService(container.Resolve<CommandRegistry>(), container);
            await commands.StartAsync();
            var greeted = Assert.Single(await commands.ExecuteAsync("greet Moongate formal"));
            Assert.Equal("Good day, Moongate.", greeted.Text);
            Assert.Equal(CommandOutputLevel.Information, greeted.Level);
            var usage = Assert.Single(await commands.ExecuteAsync("greet"));
            Assert.Equal("Usage: greet <name> [plain|warm|formal]", usage.Text);
            Assert.Equal(CommandOutputLevel.Error, usage.Level);
            var undefinedTone = Assert.Single(await commands.ExecuteAsync("greet Bob 7"));
            Assert.Equal("Usage: greet <name> [plain|warm|formal]", undefinedTone.Text);
            Assert.Equal(CommandOutputLevel.Error, undefinedTone.Level);
            await commands.StopAsync();

            var provider = Assert.Single(
                container.ResolveMany<IMetricProvider>(),
                candidate => candidate.ProviderName == "greeter"
            );
            using var diagnostics = new DiagnosticServiceFixture([provider]);
            await diagnostics.Service.StartAsync();
            var snapshot = await diagnostics.NextAsync();
            Assert.Empty(snapshot.FailedProviders);
            Assert.Equal(2, snapshot.Metrics["greeter.hello_calls"].Value);

            var definitions = await File.ReadAllTextAsync(Path.Combine(files.Directories["scripts"], "definitions.lua"));
            Assert.Contains("---@class greeter", definitions, StringComparison.Ordinal);
            Assert.Contains("---@enum Tone", definitions, StringComparison.Ordinal);
        }
        finally
        {
            await bootstrap.StopAsync().WaitAsync(Timeout);
        }
    }
}
