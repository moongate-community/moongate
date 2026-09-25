using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Extensions.Container;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Server.Core.Extensions;

public static class ContainerExtensions
{
    extension(Container container)
    {
        /// <summary>Registers a singleton implementation and its startup metadata.</summary>
        public Container AddMoongateService<TService, TImplementation>(int priority = 0)
            where TService : class
            where TImplementation : class, TService
        {
            return container.AddMoongateService(typeof(TService), typeof(TImplementation), priority);
        }

        /// <summary>Registers a concrete singleton service as itself.</summary>
        public Container AddMoongateService<TImplementation>(int priority = 0)
            where TImplementation : class
        {
            return container.AddMoongateService(typeof(TImplementation), priority);
        }

        /// <summary>Registers a singleton service using types known at runtime.</summary>
        public Container AddMoongateService(Type serviceType, Type implementationType, int priority = 0)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(serviceType);
            ArgumentNullException.ThrowIfNull(implementationType);

            container.Register(serviceType, implementationType, Reuse.Singleton);

            return RecordRegistration(container, serviceType, implementationType, priority);
        }

        /// <summary>Registers a concrete singleton service as itself using its runtime type.</summary>
        public Container AddMoongateService(Type implementationType, int priority = 0)
        {
            return container.AddMoongateService(implementationType, implementationType, priority);
        }

        /// <summary>Registers an existing instance and records its actual implementation type.</summary>
        public Container AddMoongateService<TService>(TService instance, int priority = 0)
            where TService : class
        {
            return container.AddMoongateService(typeof(TService), instance, priority);
        }

        /// <summary>Registers an existing implementation under a service contract.</summary>
        public Container AddMoongateService<TService, TImplementation>(TImplementation instance, int priority = 0)
            where TService : class
            where TImplementation : class, TService
        {
            return container.AddMoongateService<TService>(instance, priority);
        }

        /// <summary>Registers an existing instance under a service type known at runtime.</summary>
        public Container AddMoongateService(Type serviceType, object instance, int priority = 0)
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(serviceType);
            ArgumentNullException.ThrowIfNull(instance);

            container.RegisterInstance(serviceType, instance);

            return RecordRegistration(container, serviceType, instance.GetType(), priority);
        }

        /// <summary>Registers a lazy singleton factory with access to the dependency resolver.</summary>
        /// <remarks>The concrete implementation type determines startup metadata before the factory runs.</remarks>
        public Container AddMoongateService<TService, TImplementation>(
            Func<IResolverContext, TImplementation> factory,
            int priority = 0
        )
            where TService : class
            where TImplementation : class, TService
        {
            return container.AddMoongateService(typeof(TService), typeof(TImplementation), factory, priority);
        }

        /// <summary>Registers a concrete service as itself using a lazy factory with access to the dependency resolver.</summary>
        public Container AddMoongateService<TImplementation>(
            Func<IResolverContext, TImplementation> factory,
            int priority = 0
        )
            where TImplementation : class
        {
            return container.AddMoongateService<TImplementation, TImplementation>(factory, priority);
        }

        /// <summary>Registers a lazy singleton factory without resolver parameters.</summary>
        /// <remarks>The concrete implementation type determines startup metadata before the factory runs.</remarks>
        public Container AddMoongateService<TService, TImplementation>(Func<TImplementation> factory, int priority = 0)
            where TService : class
            where TImplementation : class, TService
        {
            return container.AddMoongateService(typeof(TService), typeof(TImplementation), factory, priority);
        }

        /// <summary>Registers a concrete service as itself using a lazy factory without resolver parameters.</summary>
        public Container AddMoongateService<TImplementation>(Func<TImplementation> factory, int priority = 0)
            where TImplementation : class
        {
            return container.AddMoongateService<TImplementation, TImplementation>(factory, priority);
        }

        /// <summary>Registers a concrete runtime type as itself using a lazy factory with access to the dependency resolver.</summary>
        public Container AddMoongateService(
            Type implementationType,
            Func<IResolverContext, object> factory,
            int priority = 0
        )
        {
            return container.AddMoongateService(implementationType, implementationType, factory, priority);
        }

        /// <summary>Registers a concrete runtime type as itself using a lazy factory without resolver parameters.</summary>
        public Container AddMoongateService(Type implementationType, Func<object> factory, int priority = 0)
        {
            return container.AddMoongateService(implementationType, implementationType, factory, priority);
        }

        /// <summary>Registers a lazy singleton factory using explicit runtime service and implementation types.</summary>
        public Container AddMoongateService(
            Type serviceType,
            Type implementationType,
            Func<object> factory,
            int priority = 0
        )
        {
            ArgumentNullException.ThrowIfNull(factory);

            return container.AddMoongateService(serviceType, implementationType, _ => factory(), priority);
        }

        /// <summary>Registers a lazy singleton factory with a dependency resolver and explicit runtime types.</summary>
        /// <remarks>
        /// The implementation type must be a closed, concrete class so startup metadata is available without invoking the factory.
        /// The factory must return an instance compatible with that implementation type.
        /// </remarks>
        public Container AddMoongateService(
            Type serviceType,
            Type implementationType,
            Func<IResolverContext, object> factory,
            int priority = 0
        )
        {
            ArgumentNullException.ThrowIfNull(container);
            ArgumentNullException.ThrowIfNull(serviceType);
            ArgumentNullException.ThrowIfNull(implementationType);
            ArgumentNullException.ThrowIfNull(factory);

            if (!implementationType.IsClass || implementationType.IsAbstract || implementationType.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    "Factory implementation type must be a closed, concrete class.",
                    nameof(implementationType)
                );
            }

            if (!serviceType.IsAssignableFrom(implementationType))
            {
                throw new ArgumentException(
                    "Implementation type must be assignable to the service type.",
                    nameof(implementationType)
                );
            }

            container.RegisterDelegate(
                serviceType,
                resolver =>
                {
                    var instance = factory(resolver);

                    if (!implementationType.IsInstanceOfType(instance))
                    {
                        throw new InvalidOperationException($"The factory must return an instance of {implementationType}.");
                    }

                    return instance;
                },
                Reuse.Singleton
            );

            return RecordRegistration(container, serviceType, implementationType, priority);
        }

        public DirectoriesConfig GetDirectoriesConfig()
        {
            return !container.IsRegistered<DirectoriesConfig>()
                           ? throw new InvalidOperationException("DirectoriesConfig is not registered in the container.")
                           : container.Resolve<DirectoriesConfig>();
        }
    }

    private static Container RecordRegistration(Container container, Type serviceType, Type implementationType, int priority)
    {
        var isAutostart = typeof(IMoongateStartupService).IsAssignableFrom(implementationType);

        container.AddToRegisterTypedList(
            new ServiceRegistrationData(serviceType, implementationType, isAutostart, priority)
        );

        return container;
    }
}
