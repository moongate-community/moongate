using System.Runtime.ExceptionServices;

using DryIoc;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Interfaces.Bootstrap;
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
    private Task? _startTask;
    private Task? _stopTask;

    public MoongateServerBootstrap(Container container, CancellationToken cancellationToken)
    {
        _container = container;
        _cancellationToken = cancellationToken;
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
            return _stopTask ??= StopCoreAsync();
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
        var registrations = _container.Resolve<List<ServiceRegistrationData>>()
            .Where(registration => registration.IsAutostart)
            .OrderBy(registration => registration.Priority)
            .ToArray();

        try
        {
            foreach (var registration in registrations)
            {
                var service = (IMoongateStartupService)_container.Resolve(registration.ServiceType);
                if (!_knownServices.Add(service)) continue;

                _startedServices.Add(service);
                await service.StartAsync().ConfigureAwait(false);
            }

            _logger.Information("Moongate Server started.");
        }
        catch (Exception exception)
        {
            var cleanupFailures = await StopStartedServicesAsync().ConfigureAwait(false);
            if (cleanupFailures.Count == 0)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            cleanupFailures.Insert(0, exception);
            throw new AggregateException(cleanupFailures);
        }
    }

    private async Task StopCoreAsync()
    {
        var failures = await StopStartedServicesAsync().ConfigureAwait(false);

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

        ThrowFailures(failures);
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
