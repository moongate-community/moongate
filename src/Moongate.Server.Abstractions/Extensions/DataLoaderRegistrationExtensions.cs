using DryIoc;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Loading;
using SquidStd.Abstractions.Extensions.Container;

namespace Moongate.Server.Abstractions.Extensions;

/// <summary>
/// Registers startup data loaders. Loaders are collected in a typed list and executed in ascending
/// <c>priority</c> order by the data loader pipeline the server wires at startup.
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
        container.AddToRegisterTypedList(new DataLoaderRegistration(priority, static resolver => resolver.Resolve<T>()));

        return container;
    }
}
