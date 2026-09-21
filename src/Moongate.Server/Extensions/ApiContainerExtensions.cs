using DryIoc;
using Moongate.Api.Registry;

namespace Moongate.Server.Extensions;

/// <summary>Registers container-owned API handlers during single-threaded server composition.</summary>
public static class ApiContainerExtensions
{
    /// <summary>Infers the operation contract and registers a deferred singleton handler in this container's registry.</summary>
    /// <remarks>Validation precedes mutation. Freeze the registry before starting API endpoints to resolve handler constructors.</remarks>
    public static Container RegisterApiHandler<THandler>(this Container container) where THandler : class
    {
        ArgumentNullException.ThrowIfNull(container);
        var registered = container.IsRegistered<ApiRegistry>();
        var registry = registered ? container.Resolve<ApiRegistry>() : new();
        registry.ValidateHandler(typeof(THandler));

        if (container.IsRegistered<THandler>())
        {
            throw new InvalidOperationException("The handler already has a container registration.");
        }

        container.Register<THandler>(Reuse.Singleton);
        registry.RegisterHandler(() => container.Resolve<THandler>());

        if (!registered)
        {
            container.RegisterInstance(registry);
        }

        return container;
    }
}
