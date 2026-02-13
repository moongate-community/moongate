using DryIoc;
using Moongate.Core.Server.Data.Internal.Services;

namespace Moongate.Core.Server.Extensions;

public static class ServiceRegistrationExtension
{
    public static IContainer AddService(
        this IContainer container,
        Type serviceType,
        Type implementationType,
        int priority = 0
    )
    {
        ArgumentNullException.ThrowIfNull(container);

        ArgumentNullException.ThrowIfNull(serviceType);

        ArgumentNullException.ThrowIfNull(implementationType);

        container.Register(serviceType, implementationType, Reuse.Singleton);

        container.AddToRegisterTypedList(new ServiceDefinitionObject(serviceType, implementationType, priority));

        return container;
    }

    public static IContainer AddService(this IContainer container, Type serviceType, int priority = 0)
        => container.AddService(serviceType, serviceType, priority);
}
