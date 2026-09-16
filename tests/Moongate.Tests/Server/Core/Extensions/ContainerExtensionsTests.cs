using DryIoc;
using Moongate.Server.Core.Data.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Tests.TestSupport.Services;
using Moongate.Tests.TestSupport.Services.Interfaces;

namespace Moongate.Tests.Server.Core.Extensions;

public class ContainerExtensionsTests
{
    [Theory,
     InlineData("generic-mapping", true),
     InlineData("generic-self", false),
     InlineData("runtime-mapping", true),
     InlineData("runtime-self", false)]
    public void Register_ByType_RegistersSingletonWithDependenciesAndStartupMetadata(string overload, bool mapped)
    {
        using var container = new Container();
        var dependency = new RegistrationDependency();
        container.RegisterInstance(dependency);
        var contractType = typeof(IRegistrationService);
        var implementationType = typeof(StartupRegistrationService);

        var returned = overload switch
        {
            "generic-mapping" => container.RegisterMoongateService<IRegistrationService, StartupRegistrationService>(7),
            "generic-self" => container.RegisterMoongateService<StartupRegistrationService>(7),
            "runtime-mapping" => container.RegisterMoongateService(contractType, implementationType, 7),
            "runtime-self" => container.RegisterMoongateService(implementationType, 7),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        };

        var serviceType = mapped ? typeof(IRegistrationService) : typeof(StartupRegistrationService);
        var service = Assert.IsType<StartupRegistrationService>(container.Resolve(serviceType));
        Assert.Same(container, returned);
        Assert.Same(service, container.Resolve(serviceType));
        Assert.Same(dependency, service.Dependency);
        Assert.Equal(
            new ServiceRegistrationData(serviceType, typeof(StartupRegistrationService), true, 7),
            Assert.Single(container.Resolve<List<ServiceRegistrationData>>())
        );
    }

    [Theory,
     InlineData("generic-service", true),
     InlineData("generic-mapping", true),
     InlineData("inferred-self", false),
     InlineData("runtime-service", true)]
    public void Register_Instance_UsesExistingObjectAndItsActualType(string overload, bool mapped)
    {
        using var container = new Container();
        var instance = new StartupRegistrationService(new RegistrationDependency());
        IRegistrationService service = instance;
        var contractType = typeof(IRegistrationService);

        var returned = overload switch
        {
            "generic-service" => container.RegisterMoongateService(service, -2),
            "generic-mapping" => container.RegisterMoongateService<IRegistrationService, StartupRegistrationService>(instance, -2),
            "inferred-self" => container.RegisterMoongateService(instance, -2),
            "runtime-service" => container.RegisterMoongateService(contractType, instance, -2),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        };

        var serviceType = mapped ? typeof(IRegistrationService) : typeof(StartupRegistrationService);
        Assert.Same(container, returned);
        Assert.Same(instance, container.Resolve(serviceType));
        Assert.Same(instance, container.Resolve(serviceType));
        Assert.Equal(
            new ServiceRegistrationData(serviceType, typeof(StartupRegistrationService), true, -2),
            Assert.Single(container.Resolve<List<ServiceRegistrationData>>())
        );
    }

    [Theory,
     InlineData("generic-mapping-context", true),
     InlineData("generic-mapping-parameterless", true),
     InlineData("generic-self-context", false),
     InlineData("generic-self-parameterless", false),
     InlineData("inferred-self-context", false),
     InlineData("inferred-self-parameterless", false),
     InlineData("runtime-mapping-context", true),
     InlineData("runtime-mapping-parameterless", true),
     InlineData("runtime-self-context", false),
     InlineData("runtime-self-parameterless", false)]
    public void Register_Factory_RemainsLazyAndCreatesOneInstanceWithStartupMetadata(string overload, bool mapped)
    {
        using var container = new Container();
        var dependency = new RegistrationDependency();
        container.RegisterInstance(dependency);
        var calls = 0;

        StartupRegistrationService Create(RegistrationDependency resolvedDependency)
        {
            calls++;

            return new StartupRegistrationService(resolvedDependency);
        }

        var returned = overload switch
        {
            "generic-mapping-context" => container.RegisterMoongateService<IRegistrationService, StartupRegistrationService>(
                resolver => Create(resolver.Resolve<RegistrationDependency>()), 3
            ),
            "generic-mapping-parameterless" => container.RegisterMoongateService<IRegistrationService, StartupRegistrationService>(
                () => Create(dependency), 3
            ),
            "generic-self-context" => container.RegisterMoongateService<StartupRegistrationService>(
                resolver => Create(resolver.Resolve<RegistrationDependency>()), 3
            ),
            "generic-self-parameterless" => container.RegisterMoongateService<StartupRegistrationService>(() => Create(dependency), 3),
            "inferred-self-context" => container.RegisterMoongateService(
                (IResolverContext resolver) => Create(resolver.Resolve<RegistrationDependency>()), 3
            ),
            "inferred-self-parameterless" => container.RegisterMoongateService(() => Create(dependency), 3),
            "runtime-mapping-context" => container.RegisterMoongateService(
                typeof(IRegistrationService), typeof(StartupRegistrationService),
                resolver => Create(resolver.Resolve<RegistrationDependency>()), 3
            ),
            "runtime-mapping-parameterless" => container.RegisterMoongateService(
                typeof(IRegistrationService), typeof(StartupRegistrationService), () => Create(dependency), 3
            ),
            "runtime-self-context" => container.RegisterMoongateService(
                typeof(StartupRegistrationService), resolver => Create(resolver.Resolve<RegistrationDependency>()), 3
            ),
            "runtime-self-parameterless" => container.RegisterMoongateService(typeof(StartupRegistrationService), () => Create(dependency), 3),
            _ => throw new ArgumentOutOfRangeException(nameof(overload))
        };

        var serviceType = mapped ? typeof(IRegistrationService) : typeof(StartupRegistrationService);
        Assert.Same(container, returned);
        Assert.Equal(0, calls);
        Assert.Equal(
            new ServiceRegistrationData(serviceType, typeof(StartupRegistrationService), true, 3),
            Assert.Single(container.Resolve<List<ServiceRegistrationData>>())
        );

        var service = Assert.IsType<StartupRegistrationService>(container.Resolve(serviceType));
        Assert.Same(service, container.Resolve(serviceType));
        Assert.Same(dependency, service.Dependency);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Register_MultipleServices_PreservesMetadataAndDefaults()
    {
        using var container = new Container();
        container.RegisterInstance(new RegistrationDependency());

        container.RegisterMoongateService<RegistrationService>()
                 .RegisterMoongateService<StartupRegistrationService>(10);

        Assert.Collection(
            container.Resolve<List<ServiceRegistrationData>>(),
            data => Assert.Equal(new ServiceRegistrationData(typeof(RegistrationService), typeof(RegistrationService), false), data),
            data => Assert.Equal(new ServiceRegistrationData(typeof(StartupRegistrationService), typeof(StartupRegistrationService), true, 10), data)
        );
        Assert.NotNull(container.Resolve<RegistrationService>());
        Assert.NotNull(container.Resolve<StartupRegistrationService>());
    }

    [Theory,
     InlineData("type", typeof(ContainerException)),
     InlineData("instance", typeof(ContainerException)),
     InlineData("factory", typeof(ArgumentException))]
    public void Register_IncompatibleTypes_LeavesContainerAndMetadataUnchanged(string overload, Type exceptionType)
    {
        using var container = new Container();

        Assert.Throws(exceptionType, () =>
        {
            switch (overload)
            {
                case "type":
                    container.RegisterMoongateService(typeof(IRegistrationService), typeof(RegistrationDependency));
                    break;
                case "instance":
                    container.RegisterMoongateService(typeof(IRegistrationService), new RegistrationDependency());
                    break;
                case "factory":
                    container.RegisterMoongateService(typeof(IRegistrationService), typeof(RegistrationDependency), () => new RegistrationDependency());
                    break;
            }
        });

        Assert.False(container.IsRegistered<IRegistrationService>());
        Assert.False(container.IsRegistered<List<ServiceRegistrationData>>());
    }

    [Fact]
    public void Register_FactoryWithoutConcreteImplementationType_FailsWithoutInvokingFactory()
    {
        using var container = new Container();
        var calls = 0;

        Assert.Throws<ArgumentException>(() => container.RegisterMoongateService<IRegistrationService>(() =>
        {
            calls++;

            return new StartupRegistrationService(new RegistrationDependency());
        }));

        Assert.Equal(0, calls);
        Assert.False(container.IsRegistered<IRegistrationService>());
        Assert.False(container.IsRegistered<List<ServiceRegistrationData>>());
    }

    [Theory, InlineData(typeof(Stream)), InlineData(typeof(List<>)), InlineData(typeof(int))]
    public void Register_FactoryWithInvalidImplementationType_DoesNotAddMetadata(Type implementationType)
    {
        using var container = new Container();

        Assert.Throws<ArgumentException>(() => container.RegisterMoongateService(typeof(object), implementationType, () => new object()));

        Assert.False(container.IsRegistered<object>());
        Assert.False(container.IsRegistered<List<ServiceRegistrationData>>());
    }

    [Fact]
    public void Register_OpenGenericMapping_ResolvesClosedSingletonServices()
    {
        using var container = new Container();

        container.RegisterMoongateService(typeof(IGenericRegistrationService<>), typeof(GenericRegistrationService<>));

        var integers = container.Resolve<IGenericRegistrationService<int>>();
        Assert.Same(integers, container.Resolve<IGenericRegistrationService<int>>());
        Assert.IsType<GenericRegistrationService<string>>(container.Resolve<IGenericRegistrationService<string>>());
        Assert.Equal(
            new ServiceRegistrationData(typeof(IGenericRegistrationService<>), typeof(GenericRegistrationService<>), false),
            Assert.Single(container.Resolve<List<ServiceRegistrationData>>())
        );
    }

    [Theory, InlineData(true), InlineData(false)]
    public void Resolve_FactoryReturnsWrongImplementationOrNull_Throws(bool returnsNull)
    {
        using var container = new Container();
        container.RegisterMoongateService(
            typeof(IRegistrationService), typeof(StartupRegistrationService),
            () => returnsNull ? null! : new RegistrationService(new RegistrationDependency())
        );

        Assert.Throws<InvalidOperationException>(() => container.Resolve<IRegistrationService>());
    }

    [Fact]
    public void Register_NullInstanceOrFactory_DoesNotAddMetadata()
    {
        using var container = new Container();

        Assert.Throws<ArgumentNullException>(() => container.RegisterMoongateService<RegistrationService>((RegistrationService)null!));
        Assert.Throws<ArgumentNullException>(() => container.RegisterMoongateService<RegistrationService>((Func<RegistrationService>)null!));
        Assert.Throws<ArgumentNullException>(() => container.RegisterMoongateService<RegistrationService>((Func<IResolverContext, RegistrationService>)null!));
        Assert.False(container.IsRegistered<RegistrationService>());
        Assert.False(container.IsRegistered<List<ServiceRegistrationData>>());
    }
}
