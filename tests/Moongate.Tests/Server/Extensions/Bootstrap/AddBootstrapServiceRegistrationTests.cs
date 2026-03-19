using DryIoc;
using Moongate.Server.Extensions.Bootstrap;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Scripting;

namespace Moongate.Tests.Server.Extensions.Bootstrap;

public class AddBootstrapServiceRegistrationTests
{
    [Test]
    public void AddBootstrapCoreAndHostedServices_ShouldRegisterScheduledEventServiceOnlyOnce()
    {
        var container = new Container();

        container.AddBootstrapCoreServices();
        container.AddBootstrapHostedServices();

        var registrations = container.GetServiceRegistrations()
                                     .Where(info => info.ServiceType == typeof(IScheduledEventService))
                                     .ToArray();

        Assert.That(registrations, Has.Length.EqualTo(1));
        Assert.That(registrations[0].ImplementationType, Is.EqualTo(typeof(ScheduledEventService)));
    }
}
