using Moongate.Server.Core.Interfaces.Bootstrap;

namespace Moongate.Tests.Support.Server;

public sealed class RecordingServerBootstrap : IMoongateServerBootstrap
{
    private readonly List<string> _events;
    private readonly Exception? _startFailure;
    private readonly Exception? _runFailure;
    private readonly Exception? _stopFailure;

    public RecordingServerBootstrap(
        List<string> events,
        Exception? startFailure = null,
        Exception? runFailure = null,
        Exception? stopFailure = null
    )
    {
        _events = events;
        _startFailure = startFailure;
        _runFailure = runFailure;
        _stopFailure = stopFailure;
    }

    public Task RunAsync()
    {
        _events.Add("run");

        return _runFailure is null ? Task.CompletedTask : Task.FromException(_runFailure);
    }

    public Task StartAsync()
    {
        _events.Add("start");

        return _startFailure is null ? Task.CompletedTask : Task.FromException(_startFailure);
    }

    public Task StopAsync()
    {
        _events.Add("stop");

        return _stopFailure is null ? Task.CompletedTask : Task.FromException(_stopFailure);
    }
}
