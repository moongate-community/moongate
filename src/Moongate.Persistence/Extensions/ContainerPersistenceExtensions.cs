using DryIoc;
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
    }
}
