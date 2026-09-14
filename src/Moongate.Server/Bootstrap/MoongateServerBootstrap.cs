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
    private readonly object _lifecycleSync = new();
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
        lock (_lifecycleSync)
        {
            return _startTask ??= StartCoreAsync();
        }
    }

    public Task StopAsync()
    {
        lock (_lifecycleSync)
        {
            return _stopTask ??= StopAfterStartupAsync(_startTask);
        }
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
        var registrations = (_container.IsRegistered<List<ServiceRegistrationData>>()
                ? _container.Resolve<List<ServiceRegistrationData>>()
                : [])
            .Where(registration => registration.IsAutostart)
            .OrderBy(registration => registration.Priority)
            .ToArray();

        try
        {
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
        lock (_lifecycleSync)
        {
            return _shutdownTask ??= ShutdownCoreAsync();
        }
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
