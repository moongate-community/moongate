using DryIoc;
using Moongate.Core.Extensions.Container;
using Moongate.Server.Ultima.Data.Internal;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Extensions;

/// <summary>
///     Registers <see cref="IDataLoader{TEntity}" /> implementations for <see cref="Services.DataLoaderService" />.
/// </summary>
public static class DataLoaderContainerExtensions
{
    /// <summary>
    ///     Registers <typeparamref name="TLoader" /> as the singleton <see cref="IDataLoader{TEntity}" /> for
    ///     <typeparamref name="TEntity" />, and schedules it to run at the given priority when the shard starts.
    /// </summary>
    public static Container AddUltimaDataLoader<TLoader, TEntity>(this Container container, int priority = 0)
        where TLoader : class, IDataLoader<TEntity>
    {
        container.Register<TLoader>(Reuse.Singleton);
        container.RegisterMapping<IDataLoader<TEntity>, TLoader>();

        var registration = new DataLoaderRegistration(
            typeof(TEntity),
            typeof(TLoader),
            priority,
            async (resolver, cancellationToken) =>
            {
                var loader = resolver.Resolve<IDataLoader<TEntity>>();
                await loader.InitializeAsync(cancellationToken);
                var result = await loader.LoadDataAsync(cancellationToken);

                return result.Entities;
            }
        );

        container.AddToRegisterTypedList(registration);

        return container;
    }
}
