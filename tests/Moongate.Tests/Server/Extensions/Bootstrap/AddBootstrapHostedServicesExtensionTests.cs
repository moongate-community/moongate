using DryIoc;
using Moongate.Abstractions.Data.Internal;
using Moongate.Abstractions.Types;
using Moongate.Server.Extensions.Bootstrap;
using Moongate.Server.Interfaces.Services.World;
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
        var registration = registrations.SingleOrDefault(
            x => x.ServiceType == typeof(ICorpseStartupCleanupService)
        );

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
}
