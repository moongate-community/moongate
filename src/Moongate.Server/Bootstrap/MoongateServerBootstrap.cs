using System.Runtime.ExceptionServices;

using DryIoc;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Bootstrap;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Bootstrap;

public class MoongateServerBootstrap : IMoongateServerBootstrap
{
    private readonly Container _container;
    private readonly ILogger _logger = Log.ForContext<MoongateServerBootstrap>();
    private readonly CancellationToken _cancellationToken;
    private readonly Lock _lifecycleSync = new();
    private readonly List<IMoongateStartupService> _startedServices = [];
    private readonly HashSet<IMoongateStartupService> _knownServices = new(ReferenceEqualityComparer.Instance);
    private readonly IMoongateEventBus _eventBus;
    private Task? _startTask;
    private Task? _stopTask;
    private Task<List<Exception>>? _shutdownTask;

    public MoongateServerBootstrap(Container container, CancellationToken cancellationToken)
    {
        _container = container;
        _cancellationToken = cancellationToken;
        _container.RegisterMoongateEventBus();
        _eventBus = _container.Resolve<IMoongateEventBus>();
    }

    public Task StartAsync()
    {
        TaskCompletionSource<Task> completion;
        Task startTask;
        lock (_lifecycleSync)
        {
            if (_startTask is not null)
            {
                return _startTask;
            }

            // Publish the shared identity before invoking callbacks; Unwrap preserves faults and cancellation.
            completion = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            startTask = _startTask = completion.Task.Unwrap();
        }

        completion.SetResult(StartCoreAsync());
        return startTask;
    }

    public Task StopAsync()
    {
        TaskCompletionSource<Task> completion;
        Task stopTask;
        Task? startupTask;
        lock (_lifecycleSync)
        {
            if (_stopTask is not null)
            {
                return _stopTask;
            }

            completion = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            startupTask = _startTask;
            stopTask = _stopTask = completion.Task.Unwrap();
        }

        completion.SetResult(StopAfterStartupAsync(startupTask));
        return stopTask;
    }

    public async Task RunAsync()
    {
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, _cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Moongate Server is shutting down due to cancellation request.");
        }
    }

    private async Task StartCoreAsync()
    {
        try
        {
            var registrations = (_container.IsRegistered<List<ServiceRegistrationData>>()
                    ? _container.Resolve<List<ServiceRegistrationData>>()
                    : [])
                .Where(registration => registration.IsAutostart)
                .OrderBy(registration => registration.Priority)
                .ToArray();

            foreach (var registration in registrations)
            {
                var service = (IMoongateStartupService)_container.Resolve(registration.ServiceType);
                if (!_knownServices.Add(service))
                {
                    continue;
                }

                _startedServices.Add(service);
                await service.StartAsync().ConfigureAwait(false);
            }

            await _eventBus.PublishAsync(new MoongateStartedEvent(), _cancellationToken).ConfigureAwait(false);
            _logger.Information("Moongate Server started.");
        }
        catch (Exception exception)
        {
            var cleanupFailures = await GetOrCreateShutdownTask().ConfigureAwait(false);
            if (cleanupFailures.Count == 0)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            cleanupFailures.Insert(0, exception);
            throw new AggregateException(cleanupFailures);
        }
    }

    private async Task StopAfterStartupAsync(Task? startupTask)
    {
        var startupFailed = false;
        if (startupTask is not null)
        {
            try
            {
                await startupTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                startupFailed = true;
            }
        }

        var failures = await GetOrCreateShutdownTask().ConfigureAwait(false);
        if (!startupFailed)
        {
            ThrowFailures(failures);
        }
    }

    private Task<List<Exception>> GetOrCreateShutdownTask()
    {
        TaskCompletionSource<Task<List<Exception>>> completion;
        Task<List<Exception>> shutdownTask;
        lock (_lifecycleSync)
        {
            if (_shutdownTask is not null)
            {
                return _shutdownTask;
            }

            completion = new TaskCompletionSource<Task<List<Exception>>>(TaskCreationOptions.RunContinuationsAsynchronously);
            shutdownTask = _shutdownTask = completion.Task.Unwrap();
        }

        completion.SetResult(ShutdownCoreAsync());
        return shutdownTask;
    }

    private async Task<List<Exception>> ShutdownCoreAsync()
    {
        List<Exception> failures = [];

        try
        {
            await _eventBus.PublishAsync(new MoongateStoppingEvent(), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        failures.AddRange(await StopStartedServicesAsync().ConfigureAwait(false));

        try
        {
            await _eventBus.PublishAsync(new MoongateStoppedEvent(), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            _container.Dispose();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        _logger.Information("Moongate Server stopped.");

        try
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        return failures;
    }

    private async Task<List<Exception>> StopStartedServicesAsync()
    {
        List<Exception> failures = [];
        var services = _startedServices.ToArray();
        _startedServices.Clear();

        for (var index = services.Length - 1; index >= 0; index--)
        {
            try
            {
                await services[index].StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static void ThrowFailures(List<Exception> failures)
    {
        if (failures.Count == 0) return;
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException(failures);
    }
}
