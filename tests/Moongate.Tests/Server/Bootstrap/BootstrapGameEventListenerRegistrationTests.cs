using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Services.Interaction;
using Moongate.Server.Services.Persistence;

namespace Moongate.Tests.Server.Bootstrap;

public class BootstrapGameEventListenerRegistrationTests
{
    [Test]
    public void AllIMoongateServiceEventListeners_ShouldHaveRegisterGameEventListenerAttribute()
    {
        var serverAssembly = typeof(MobileHandler).Assembly;

        var moongateServiceListeners = serverAssembly.GetTypes()
            .Where(
                type => !type.IsAbstract
                        && !type.IsInterface
                        && typeof(IMoongateService).IsAssignableFrom(type)
                        && type.GetInterfaces().Any(IsGameEventListenerInterface)
            )
            .ToArray();

        var missingAttribute = moongateServiceListeners
            .Where(
                type => type.GetCustomAttributes(typeof(RegisterGameEventListenerAttribute), false).Length == 0
            )
            .Select(static type => type.FullName)
            .ToArray();

        Assert.That(
            missingAttribute,
            Is.Empty,
            "All IMoongateService types implementing IGameEventListener<T> should have [RegisterGameEventListener]"
        );
    }

    [Test]
    public void MobileHandler_ShouldImplementIMoongateService()
    {
        Assert.That(typeof(IMoongateService).IsAssignableFrom(typeof(MobileHandler)), Is.True);
    }

    [Test]
    public void MobileHandler_ShouldNotDependOnIGameEventBusService()
    {
        var constructors = typeof(MobileHandler).GetConstructors();

        var hasGameEventBusDependency = constructors.Any(
            ctor => ctor.GetParameters().Any(p => p.ParameterType == typeof(IGameEventBusService))
        );

        Assert.That(
            hasGameEventBusDependency,
            Is.False,
            "MobileHandler should no longer depend on IGameEventBusService for self-registration"
        );
    }

    [Test]
    public void PersistenceListenerHandler_ShouldHaveDefaultPriority()
    {
        var attribute = GetAttribute<PersistenceListenerHandler>();

        Assert.That(attribute.Priority, Is.EqualTo(200));
    }

    [Test]
    public void PlayerTargetService_ShouldHaveDefaultPriority()
    {
        var attribute = GetAttribute<PlayerTargetService>();

        Assert.That(attribute.Priority, Is.EqualTo(200));
    }

    [Test]
    public void MobileHandler_ShouldHaveDefaultPriority()
    {
        var attribute = GetAttribute<MobileHandler>();

        Assert.That(attribute.Priority, Is.EqualTo(200));
    }

    private static RegisterGameEventListenerAttribute GetAttribute<T>()
    {
        var attribute = typeof(T)
            .GetCustomAttributes(typeof(RegisterGameEventListenerAttribute), false)
            .Cast<RegisterGameEventListenerAttribute>()
            .SingleOrDefault();

        Assert.That(attribute, Is.Not.Null, $"{typeof(T).Name} should have [RegisterGameEventListener]");

        return attribute!;
    }

    private static bool IsGameEventListenerInterface(Type interfaceType)
        => interfaceType.IsGenericType
           && interfaceType.GetGenericTypeDefinition() == typeof(IGameEventListener<>);
}
