using DryIoc;
using Moongate.Http.Plugin.Interfaces.Endpoints;

namespace Moongate.Tests.Support;

/// <summary>Registers an already-built endpoint group, which the production extension cannot do.</summary>
public static class ApiEndpointInstanceExtensions
{
    /// <summary>
    /// Appends rather than replaces: <see cref="Registrator.RegisterInstance{TService}" /> would
    /// otherwise keep only the last group registered, and a test wiring several would silently lose all
    /// but one.
    /// </summary>
    public static IContainer RegisterApiEndpointInstance(this IContainer container, IApiEndpointRegistration endpoints)
    {
        container.RegisterInstance(endpoints, IfAlreadyRegistered.AppendNotKeyed);

        return container;
    }
}
