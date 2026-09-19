using System.Reflection;
using DryIoc;
using Moongate.Core.Attributes.Entities;
using Moongate.Core.Interfaces.Entities;
using Moongate.Persistence.Data;
using Moongate.Persistence.DataAccess;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;

namespace Moongate.Persistence.Extensions;

public static class ContainerPersistenceExtensions
{
    extension(Container container)
    {
        public Container RegisterMoongatePersistence(
            string directory, PersistenceOptions? options = null
        )
        {
            ArgumentNullException.ThrowIfNull(container);
            var persistence = new MoongatePersistenceService(directory, options);
            container.RegisterInstance(persistence);

            return container;
        }

        public Container RegisterDataAccess<T>(string collectionName)
            where T : class, IMoongateEntity
        {
            ArgumentNullException.ThrowIfNull(container);
            var dataAccess = container.Resolve<MoongatePersistenceService>().Register<T>(collectionName);
            var setup = Setup.With(preventDisposal: true);
            container.RegisterInstance(dataAccess, setup: setup);
            container.RegisterInstance<IDataAccess<T>>(dataAccess, setup: setup);

            return container;
        }

        /// <summary>Registers a typed collection whose live source is captured by SaveAllAsync.</summary>
        /// <remarks>
        /// Register before startup. The source must support synchronized enumeration; missing entities
        /// are not deleted. The same data access instance remains available for explicit reads and writes.
        /// </remarks>
        public Container RegisterDataAccess<T>(string collectionName, Func<IEnumerable<T>> entitySource)
            where T : class, IMoongateEntity
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(entitySource);
            var dataAccess = container.Resolve<MoongatePersistenceService>().Register(collectionName, entitySource);
            var setup = Setup.With(preventDisposal: true);
            container.RegisterInstance(dataAccess, setup: setup);
            container.RegisterInstance<IDataAccess<T>>(dataAccess, setup: setup);

            return container;
        }

        /// <summary>Registers a typed collection whose name the entity declares for itself.</summary>
        /// <remarks>
        /// Equivalent to <see cref="RegisterDataAccess{T}(string)"/> with the name taken from the entity's
        /// <see cref="PersistenceCollectionAttribute"/>, so no call site repeats it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The entity declares no collection.</exception>
        public Container AddPersistenceEntity<T>()
            where T : class, IMoongateEntity
        {
            return container.RegisterDataAccess<T>(GetCollectionName<T>());
        }

        /// <summary>
        /// Registers a typed collection the entity names for itself, together with the live source that
        /// SaveAllAsync captures.
        /// </summary>
        /// <remarks>
        /// Equivalent to <see cref="RegisterDataAccess{T}(string, Func{IEnumerable{T}})"/>, and carries the
        /// same requirements: register before startup, and synchronize enumeration against mutation.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The entity declares no collection.</exception>
        public Container AddPersistenceEntity<T>(Func<IEnumerable<T>> entitySource)
            where T : class, IMoongateEntity
        {
            return container.RegisterDataAccess(GetCollectionName<T>(), entitySource);
        }
    }

    private static string GetCollectionName<T>()
        where T : class, IMoongateEntity
    {
        var attribute = typeof(T).GetCustomAttribute<PersistenceCollectionAttribute>(inherit: false);

        if (attribute is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(T).FullName}' carries no {nameof(PersistenceCollectionAttribute)}. Declare the "
                + "collection on the entity, or register it with an explicit name through RegisterDataAccess."
            );
        }

        return attribute.Name;
    }
}
