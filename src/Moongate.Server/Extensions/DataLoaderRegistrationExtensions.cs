using DryIoc;
using Moongate.Server.Data.Internal;
using Moongate.Server.Interfaces.Loading;
using Moongate.Server.Services.Loading;
using SquidStd.Abstractions.Extensions.Container;
using SquidStd.Abstractions.Extensions.Services;

namespace Moongate.Server.Extensions;

/// <summary>
/// Registers startup data loaders and the pipeline service that runs them. Loaders are collected in
/// a typed list and executed in ascending <c>priority</c> order, mirroring SquidStd's persistence
/// seeder registration.
/// </summary>
public static class DataLoaderRegistrationExtensions
{
    /// <summary>
    /// Records a data loader type; it is resolved as a singleton and run at startup in ascending
    /// <paramref name="priority" /> order.
    /// </summary>
    public static IContainer RegisterDataLoader<T>(this IContainer container, int priority = 0)
        where T : class, IDataLoader
    {
        container.Register<T>(Reuse.Singleton);
        container.AddToRegisterTypedList(
            new DataLoaderRegistration(priority, static resolver => resolver.Resolve<T>())
        );

        return container;
    }

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
