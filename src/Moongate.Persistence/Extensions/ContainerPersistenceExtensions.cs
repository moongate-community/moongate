using DryIoc;
using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data.Config;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Extensions;

/// <summary>
///     Registers persistence modules and typed facades without database I/O.
/// </summary>
public static class ContainerPersistenceExtensions
{
    extension(Container container)
    {
        /// <summary>
        ///     Registers an entity in the shared authentication database.
        /// </summary>
        public Container AddPersistenceAuth<T>() where T : class, IMoongateEntity
        {
            return RegisterEntity<T>(container, null, null, PersistenceDatabaseTarget.Accounts);
        }

        /// <summary>
        ///     Registers authentication entities with an explicit detached snapshot function.
        /// </summary>
        public Container AddPersistenceAuth<T>(Func<IEnumerable<T>> source, Func<T, T> snapshot)
            where T : class, IMoongateEntity
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(snapshot);

            return RegisterEntity(container, source, snapshot, PersistenceDatabaseTarget.Accounts);
        }

        /// <summary>
        ///     Registers a singleton typed facade whose module ownership is resolved after the full batch.
        /// </summary>
        public Container AddPersistenceEntity<T>() where T : class, IMoongateEntity
        {
            return RegisterEntity<T>(container, null, null);
        }

        /// <summary>
        ///     Registers a live source with an explicit detached snapshot function.
        /// </summary>
        /// <remarks>
        ///     The function must copy all mutable nested state. Sources are captured only inside SaveAllAsync's
        ///     owner callback; missing entities are not deleted. Registration freezes when schema preparation starts.
        /// </remarks>
        public Container AddPersistenceEntity<T>(Func<IEnumerable<T>> source, Func<T, T> snapshot)
            where T : class, IMoongateEntity
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(snapshot);

            return RegisterEntity(container, source, snapshot);
        }

        /// <summary>
        ///     Constructs a module through DryIoc and adds its declaration to the registration batch.
        /// </summary>
        public Container AddPersistenceModule<TModule>() where TModule : class, IPersistenceModule
        {
            ArgumentNullException.ThrowIfNull(container);

            if (container.IsRegistered<TModule>())
            {
                throw new InvalidOperationException(
                    $"Persistence module '{typeof(TModule).FullName}' is already registered."
                );
            }

            container.Register<TModule>(Reuse.Singleton);
            container.Resolve<MoongatePersistenceService>().RegisterModule(container.Resolve<TModule>());

            return container;
        }

        /// <summary>
        ///     Registers an entity in this world's database.
        /// </summary>
        public Container AddPersistenceWorld<T>() where T : class, IMoongateEntity
        {
            return RegisterEntity<T>(container, null, null, PersistenceDatabaseTarget.Realm);
        }

        /// <summary>
        ///     Registers world entities with an explicit detached snapshot function.
        /// </summary>
        public Container AddPersistenceWorld<T>(Func<IEnumerable<T>> source, Func<T, T> snapshot)
            where T : class, IMoongateEntity
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(snapshot);

            return RegisterEntity(container, source, snapshot, PersistenceDatabaseTarget.Realm);
        }

        /// <summary>
        ///     Registers the shared persistence owner.
        /// </summary>
        public Container RegisterMoongatePersistence(PostgreSqlPersistenceOptions options)
        {
            ArgumentNullException.ThrowIfNull(container);

            if (container.IsRegistered<MoongatePersistenceService>())
            {
                throw new InvalidOperationException("Persistence is already registered.");
            }

            container.RegisterInstance(new MoongatePersistenceService(options));

            return container;
        }
    }

    private static Container RegisterEntity<T>(
        Container container,
        Func<IEnumerable<T>>? source,
        Func<T, T>? snapshot,
        PersistenceDatabaseTarget? target = null
    ) where T : class, IMoongateEntity
    {
        ArgumentNullException.ThrowIfNull(container);
        var facade = container.Resolve<MoongatePersistenceService>().RegisterEntity(source, snapshot, target);
        var setup = Setup.With(preventDisposal: true);
        container.RegisterInstance(facade, setup: setup);
        container.RegisterInstance<IDataAccess<T>>(facade, setup: setup);

        return container;
    }
}
