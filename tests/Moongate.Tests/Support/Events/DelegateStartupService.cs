using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.Support.Events;

public sealed class DelegateStartupService : IMoongateStartupService
{
    private readonly Func<Task> _start;
    private readonly Func<Task> _stop;

    public DelegateStartupService(Func<Task> start, Func<Task> stop)
    {
        _start = start;
        _stop = stop;
    }

    public Task StartAsync()
    {
        return _start();
    }

    public Task StopAsync()
    {
        return _stop();
    }
}
