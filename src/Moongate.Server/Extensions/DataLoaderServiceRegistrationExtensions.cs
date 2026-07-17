using DryIoc;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Services.Loading;
using SquidStd.Abstractions.Extensions.Services;

namespace Moongate.Server.Extensions;

/// <summary>
/// Registers the pipeline service that runs the recorded data loaders. The per-loader seam
/// (<c>RegisterDataLoader</c>) lives in Moongate.Server.Abstractions so plugins can use it; wiring
/// the concrete pipeline is the composition root's job and stays here.
/// </summary>
public static class DataLoaderServiceRegistrationExtensions
{
    /// <summary>
    /// Registers the <see cref="IDataLoaderService" /> pipeline: the priority-ordered loader list plus
    /// the manager as a std service started at <paramref name="priority" /> (default 110, after the
    /// client-files locator).
    /// </summary>
    public static IContainer RegisterDataLoaderService(this IContainer container, int priority = 110)
    {
        container.RegisterDelegate<IReadOnlyList<IDataLoader>>(
            static resolver =>
            {
                var recorded = resolver.Resolve<List<DataLoaderRegistration>>(IfUnresolved.ReturnDefault);

                return recorded is null
                    ? []
                    : recorded.OrderBy(registration => registration.Priority)
                        .Select(registration => registration.Resolve(resolver))
                        .ToList();
            },
            Reuse.Singleton
        );

        return container.RegisterStdService<IDataLoaderService, DataLoaderService>(priority);
    }
}
