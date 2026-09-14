namespace Moongate.Tests.Support.Server;

public sealed class RecordingStartupService : IRecordingStartupService, ISecondaryRecordingStartupService
{
    private readonly string _name;
    private readonly List<string> _events;
    private readonly Action? _onStart;
    private readonly Exception? _startFailure;
    private readonly Exception? _stopFailure;

    public RecordingStartupService(
        string name, List<string> events, Action? onStart = null,
        Exception? startFailure = null, Exception? stopFailure = null
    )
    {
        _name = name;
        _events = events;
        _onStart = onStart;
        _startFailure = startFailure;
        _stopFailure = stopFailure;
    }

    public Task StartAsync()
    {
        _events.Add($"start:{_name}");
        _onStart?.Invoke();

        return _startFailure is null ? Task.CompletedTask : Task.FromException(_startFailure);
    }

    public Task StopAsync()
    {
        _events.Add($"stop:{_name}");

        return _stopFailure is null ? Task.CompletedTask : Task.FromException(_stopFailure);
    }
}
