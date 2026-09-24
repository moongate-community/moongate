using System.Diagnostics;
using DryIoc;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Resolves autostart services in priority order and stops attempted services in reverse order.
/// Lifecycle task coordination is owned by the bootstrap.
/// </summary>
internal sealed class StartupServiceLifecycle
{
    private readonly Container _container;
    private readonly ILogger _logger = Log.ForContext<StartupServiceLifecycle>();
    private readonly List<IMoongateStartupService> _startedServices = [];
    private readonly HashSet<IMoongateStartupService> _knownServices = new(ReferenceEqualityComparer.Instance);

    public StartupServiceLifecycle(Container container)
    {
        _container = container;
    }

    public void ActivateAdministration()
    {
        foreach (var service in _startedServices.OfType<IAdminApiService>()) { service.Activate(); }
    }

    public void StopAcceptingAdministration()
    {
        foreach (var service in _startedServices.OfType<IAdminApiService>()) { service.StopAccepting(); }
    }

    public void ActivateWorldSaving()
    {
        foreach (var service in _startedServices.OfType<IWorldSaveService>())
        {
            service.Activate();
        }
    }

    public async Task StartAsync(Action<IMoongateStartupService>? onStarting = null)
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
            onStarting?.Invoke(service);

            var serviceName = service.GetType().Name;
            _logger.Debug("Starting service {ServiceName:l}", serviceName);
            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                await service.StartAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to start service {ServiceName:l}", serviceName);

                throw;
            }

            _logger.Information(
                "Service {ServiceName:l} started in {ElapsedMilliseconds:F2} ms",
                serviceName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
        }
    }

    public async Task<List<Exception>> StopAsync(bool saveWorld = false)
    {
        List<Exception> failures = [];
        var services = _startedServices.ToArray();
        _startedServices.Clear();

        for (var index = services.Length - 1; index >= 0; index--)
        {
            try
            {
                if (services[index] is IWorldSaveService worldSave)
                {
                    await worldSave.StopAsync(saveWorld).ConfigureAwait(false);
                }
                else
                {
                    await services[index].StopAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }
}
