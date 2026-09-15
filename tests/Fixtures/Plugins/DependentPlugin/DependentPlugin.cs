using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Tests.Fixtures.Plugins.PrivateDependency;

namespace Moongate.Tests.Fixtures.Plugins.DependentPlugin;

public sealed class DependentPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new(
        "loader.dependent", "Dependent", new Version(1, 0),
        dependencies: [new MoongatePluginDependencyData("loader.foundation")]);

    public void Register(Container container)
    {
        var events = container.Resolve<List<string>>();
        events.Add($"dependent:register:{PluginMessage.GetMessage()}");
        container.RegisterMoongateService<IMoongateStartupService, PluginStartupService>();
        container.OnEvent<MoongateStartedEvent>((_, _) => RecordAsync(events, "dependent:started"))
            .OnEvent<MoongateStoppingEvent>((_, _) => RecordAsync(events, "dependent:stopping"))
            .OnEvent<MoongateStoppedEvent>((_, _) => RecordAsync(events, "dependent:stopped"));
    }

    private static Task RecordAsync(List<string> events, string message)
    {
        events.Add(message);
        return Task.CompletedTask;
    }
}
