using DryIoc;

using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Resolves autostart services in priority order and stops attempted services in reverse order.
/// Lifecycle task coordination is owned by the bootstrap.
/// </summary>
internal sealed class StartupServiceLifecycle
{
    private readonly Container _container;
    private readonly List<IMoongateStartupService> _startedServices = [];
    private readonly HashSet<IMoongateStartupService> _knownServices = new(ReferenceEqualityComparer.Instance);

    public StartupServiceLifecycle(Container container)
    {
        _container = container;
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
            await service.StartAsync().ConfigureAwait(false);
        }
    }

    public async Task<List<Exception>> StopAsync()
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
}
