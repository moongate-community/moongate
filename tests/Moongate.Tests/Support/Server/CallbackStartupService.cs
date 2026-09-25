using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.Support.Server;

public sealed class CallbackStartupService : IMoongateStartupService
{
    private readonly Func<Task> _start;
    private readonly Func<Task> _stop;

    public CallbackStartupService(Func<Task> start, Func<Task> stop)
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
