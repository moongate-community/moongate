using DryIoc;
using Moongate.Abstractions.Data.Internal;
using Moongate.Abstractions.Types;
using Moongate.Server.Extensions.Bootstrap;
using Moongate.Server.Interfaces.Services.Files;
using Moongate.Server.Interfaces.Services.World;
using Moongate.Server.Services.Files;
using Moongate.Server.Services.World;

namespace Moongate.Tests.Server.Extensions.Bootstrap;

public class AddBootstrapHostedServicesExtensionTests
{
    [Test]
    public void AddBootstrapHostedServices_ShouldRegisterCorpseStartupCleanupService()
    {
        var container = new Container();

        container.AddBootstrapHostedServices();

        var registrations = container.Resolve<List<ServiceRegistrationObject>>();
        var registration = registrations.SingleOrDefault(x => x.ServiceType == typeof(ICorpseStartupCleanupService));

        Assert.Multiple(
            () =>
            {
                Assert.That(registration, Is.Not.Null);
                Assert.That(registration!.ImplementationType, Is.EqualTo(typeof(CorpseStartupCleanupService)));
                Assert.That(registration.Priority, Is.EqualTo(ServicePriority.CorpseStartupCleanup));
                Assert.That(registration.Priority, Is.GreaterThan(ServicePriority.Persistence));
                Assert.That(registration.Priority, Is.LessThan(ServicePriority.FileLoader));
            }
        );
    }

    [Test]
    public void AddBootstrapHostedServices_ShouldRegisterFileWatcherServiceAfterScriptEngine()
    {
        var container = new Container();

        container.AddBootstrapHostedServices();

        var registrations = container.Resolve<List<ServiceRegistrationObject>>();
        var registration = registrations.SingleOrDefault(x => x.ServiceType == typeof(IFileWatcherService));

        Assert.Multiple(
            () =>
            {
                Assert.That(registration, Is.Not.Null);
                Assert.That(registration!.ImplementationType, Is.EqualTo(typeof(FileWatcherService)));
                Assert.That(registration.Priority, Is.GreaterThan(ServicePriority.ScriptEngine));
                Assert.That(registration.Priority, Is.LessThan(ServicePriority.EventListener));
            }
        );
    }
}
