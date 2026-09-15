using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.Fixtures.Plugins.DependentPlugin;

public sealed class PluginStartupService : IMoongateStartupService, IDisposable
{
    private readonly List<string> _events;

    public PluginStartupService(List<string> events)
    {
        _events = events;
    }

    public Task StartAsync()
    {
        _events.Add("dependent:start");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _events.Add("dependent:stop");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _events.Add("dependent:dispose");
    }
}
