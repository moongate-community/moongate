using Moongate.Server.Attributes;
using Moongate.Server.Handlers;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Services.Persistence;

namespace Moongate.Tests.Server.Attributes;

public class RegisterGameEventListenerAttributeTests
{
    [Test]
    public void AllGameEventListenerAttributeAnnotatedClasses_ShouldNotContainDuplicates()
    {
        var serverAssembly = typeof(MobileHandler).Assembly;

        var annotatedTypes = serverAssembly.GetTypes()
                                           .Where(
                                               type => type.GetCustomAttributes(
                                                               typeof(RegisterGameEventListenerAttribute),
                                                               false
                                                           )
                                                           .Length >
                                                       0
                                           )
                                           .ToArray();

        var duplicates = annotatedTypes
                         .GroupBy(static type => type.FullName)
                         .Where(static group => group.Count() > 1)
                         .Select(static group => group.Key)
                         .ToArray();

        Assert.That(duplicates, Is.Empty);
    }

    [Test]
    public void MobileHandler_ShouldHaveRegisterGameEventListenerAttribute()
    {
        var attribute = typeof(MobileHandler)
                        .GetCustomAttributes(typeof(RegisterGameEventListenerAttribute), false)
                        .Cast<RegisterGameEventListenerAttribute>()
                        .SingleOrDefault();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute!.Priority, Is.EqualTo(200));
    }

    [Test]
    public void PersistenceListenerHandler_ShouldHaveRegisterGameEventListenerAttribute()
    {
        var attribute = typeof(PersistenceListenerHandler)
                        .GetCustomAttributes(typeof(RegisterGameEventListenerAttribute), false)
                        .Cast<RegisterGameEventListenerAttribute>()
                        .SingleOrDefault();

        Assert.That(attribute, Is.Not.Null);
    }

    [Test]
    public void PlayerTargetService_ShouldHaveRegisterGameEventListenerAttribute()
    {
        var attribute = typeof(PlayerTargetService)
                        .GetCustomAttributes(typeof(RegisterGameEventListenerAttribute), false)
                        .Cast<RegisterGameEventListenerAttribute>()
                        .SingleOrDefault();

        Assert.That(attribute, Is.Not.Null);
    }

    [Test]
    public void Priority_WhenNotSpecified_ShouldDefaultTo200()
    {
        var attribute = new RegisterGameEventListenerAttribute();

        Assert.That(attribute.Priority, Is.EqualTo(200));
    }

    [Test]
    public void Priority_WhenSpecified_ShouldReturnConfiguredValue()
    {
        var attribute = new RegisterGameEventListenerAttribute(150);

        Assert.That(attribute.Priority, Is.EqualTo(150));
    }
}
