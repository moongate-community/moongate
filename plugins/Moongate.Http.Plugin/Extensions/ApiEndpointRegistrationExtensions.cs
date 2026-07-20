using DryIoc;
using Moongate.Http.Plugin.Interfaces.Endpoints;

namespace Moongate.Http.Plugin.Extensions;

/// <summary>
/// Registers REST endpoint groups. Each one is collected under <see cref="IApiEndpointRegistration" />,
/// the typed list the HTTP server resolves and maps at startup.
/// </summary>
public static class ApiEndpointRegistrationExtensions
{
    /// <summary>
    /// Records an endpoint group as a singleton <see cref="IApiEndpointRegistration" /> so it is mapped
    /// alongside every other group.
    /// </summary>
    public static IContainer RegisterApiEndpoint<TEndpoint>(this IContainer container)
        where TEndpoint : class, IApiEndpointRegistration
    {
        container.Register<IApiEndpointRegistration, TEndpoint>(Reuse.Singleton);

        return container;
    }
}
