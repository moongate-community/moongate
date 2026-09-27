using DryIoc;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Extensions;

public sealed class ContainerScriptingExtensionsTests
{
    [Fact]
    public void RegisterScriptEnum_IsIdempotent()
    {
        using var container = new Container();

        container.RegisterScriptEnum<RegistryColour>().RegisterScriptEnum<RegistryColour>();

        Assert.Equal([typeof(RegistryColour)], container.Resolve<IScriptModuleRegistry>().EnumTypes);
    }

    [Fact]
    public void RegisterScriptModule_AppendsInOrderAndRegistersASingleton()
    {
        using var container = new Container();

        var result = container.AddScriptModule<FirstRegistryModule>().AddScriptModule<SecondRegistryModule>();

        Assert.Same(container, result);
        Assert.Equal(
            [typeof(FirstRegistryModule), typeof(SecondRegistryModule)],
            container.Resolve<IScriptModuleRegistry>().ModuleTypes
        );
        Assert.Same(container.Resolve<FirstRegistryModule>(), container.Resolve<FirstRegistryModule>());
    }

    [Fact]
    public void RegisterScriptModule_TwiceForTheSameType_Throws()
    {
        using var container = new Container();
        container.AddScriptModule<FirstRegistryModule>();

        Assert.Throws<InvalidOperationException>(() => container.AddScriptModule<FirstRegistryModule>());
    }

    [Fact]
    public void RegisterScriptModule_WithoutTheAttribute_Throws()
    {
        using var container = new Container();

        Assert.Throws<ArgumentException>(() => container.AddScriptModule<UnmarkedModule>());
    }

    [Fact]
    public void AddScriptEvent_StoresTheRegistrationUnderItsName()
    {
        using var container = new Container();

        container.AddScriptEvent<ProbeEvent>("probe_fired", e => new Dictionary<string, object?> { ["name"] = e.Name });

        var registration = Assert.Single(container.Resolve<IScriptModuleRegistry>().EventRegistrations);
        Assert.Equal("probe_fired", registration.Name);
        Assert.Equal(typeof(ProbeEvent), registration.EventType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Probe")]
    [InlineData("1probe")]
    [InlineData("probe-fired")]
    [InlineData("probe fired")]
    public void AddScriptEvent_InvalidName_Throws(string name)
    {
        using var container = new Container();

        Assert.Throws<ArgumentException>(() =>
            container.AddScriptEvent<ProbeEvent>(name, _ => new Dictionary<string, object?>())
        );
    }

    [Fact]
    public void AddScriptEvent_SameNameTwice_Throws()
    {
        using var container = new Container();
        container.AddScriptEvent<ProbeEvent>("probe_fired", _ => new Dictionary<string, object?>());

        Assert.Throws<InvalidOperationException>(() =>
            container.AddScriptEvent<OtherProbeEvent>("probe_fired", _ => new Dictionary<string, object?>())
        );
    }

    [Fact]
    public void AddScriptEvent_SameEventTypeTwice_Throws()
    {
        using var container = new Container();
        container.AddScriptEvent<ProbeEvent>("probe_fired", _ => new Dictionary<string, object?>());

        Assert.Throws<InvalidOperationException>(() =>
            container.AddScriptEvent<ProbeEvent>("probe_again", _ => new Dictionary<string, object?>())
        );
    }

    [Fact]
    public async Task AddScriptEvent_Subscribe_DeliversTheMappedValuesOfEachPublishedEvent()
    {
        using var container = new Container();
        container.RegisterMoongateEventBus();
        container.AddScriptEvent<ProbeEvent>("probe_fired", e => new Dictionary<string, object?> { ["value"] = e.Value });
        var registration = container.Resolve<IScriptModuleRegistry>().EventRegistrations.Single();
        var bus = container.Resolve<IMoongateEventBus>();
        var received = new List<object?>();

        using (registration.Subscribe(bus, map => received.Add(map()["value"])))
        {
            await bus.PublishAsync(new ProbeEvent("a", 7));
        }

        await bus.PublishAsync(new ProbeEvent("b", 8));
        Assert.Equal([7], received);
    }
}
