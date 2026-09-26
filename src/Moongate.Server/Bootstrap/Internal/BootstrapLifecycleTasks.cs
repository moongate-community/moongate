namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
///     Shares lifecycle tasks across repeated, concurrent, and reentrant calls.
///     Callback execution starts only after the corresponding task has been published.
/// </summary>
internal sealed class BootstrapLifecycleTasks
{
    private readonly Lock _lifecycleSync = new();
    private Task? _startTask;
    private Task? _stopTask;
    private Task<List<Exception>>? _shutdownTask;
    private bool _configuring;

    /// <summary>
    ///     Runs configuration exclusively before any lifecycle task has been published.
    /// </summary>
    public void Configure(Action configure)
    {
        lock (_lifecycleSync)
        {
            ThrowIfConfiguring();

            if (_startTask is not null || _stopTask is not null)
            {
                throw new InvalidOperationException("Services cannot be registered after startup or shutdown begins.");
            }

            _configuring = true;

            try
            {
                configure();
            }
            finally
            {
                _configuring = false;
            }
        }
    }

    public Task<List<Exception>> ShutdownAsync(Func<Task<List<Exception>>> shutdown)
    {
        TaskCompletionSource<Task<List<Exception>>> completion;
        Task<List<Exception>> shutdownTask;

        lock (_lifecycleSync)
        {
            if (_shutdownTask is not null)
            {
                return _shutdownTask;
            }

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdownTask = _shutdownTask = completion.Task.Unwrap();
        }

        completion.SetResult(shutdown());

        return shutdownTask;
    }

    public Task StartAsync(Func<Task> start)
    {
        TaskCompletionSource<Task> completion;
        Task startTask;

        lock (_lifecycleSync)
        {
            ThrowIfConfiguring();

            if (_startTask is not null)
            {
                return _startTask;
            }

            // Publish the shared identity before invoking callbacks; Unwrap preserves faults and cancellation.
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            startTask = _startTask = completion.Task.Unwrap();
        }

        completion.SetResult(start());

        return startTask;
    }

    public Task StopAsync(Func<Task?, Task> stop)
    {
        TaskCompletionSource<Task> completion;
        Task stopTask;
        Task? startupTask;

        lock (_lifecycleSync)
        {
            ThrowIfConfiguring();

            if (_stopTask is not null)
            {
                return _stopTask;
            }

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            startupTask = _startTask;
            stopTask = _stopTask = completion.Task.Unwrap();
        }

        completion.SetResult(stop(startupTask));

        return stopTask;
    }

    private void ThrowIfConfiguring()
    {
        if (_configuring)
        {
            throw new InvalidOperationException("Service registration cannot reenter configuration or lifecycle methods.");
        }
    }
}
