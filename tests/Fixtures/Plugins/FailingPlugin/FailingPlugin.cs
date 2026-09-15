using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Plugins;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Plugins;
using Moongate.Tests.Fixtures.Plugins.PrivateDependency;

namespace Moongate.Tests.Fixtures.Plugins.FailingPlugin;

public sealed class FailingPlugin : IMoongatePlugin
{
    public MoongatePluginData Metadata { get; } = new("loader.failing", "Failing", new Version(1, 0));

    public void Register(Container container)
    {
        var events = container.Resolve<List<string>>();
        events.Add("failing:register");
        container.OnEvent<MoongateStoppingEvent>((_, _) => StopAsync(events));
        throw new InvalidOperationException("Fixture registration failed.");
    }

    private static Task StopAsync(List<string> events)
    {
        events.Add($"failing:stopping:{PluginMessage.GetMessage()}");
        return Task.CompletedTask;
    }
}
