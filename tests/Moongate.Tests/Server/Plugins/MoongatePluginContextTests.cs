using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Server.Data.Internal.Plugins;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Tests.Server.Plugins;

public class MoongatePluginContextTests
{
    private sealed class TestPacketHandler { }

    private sealed class TestGameEventListener { }

    private sealed class TestCommand { }

    private sealed class TestFileLoader { }

    private sealed class TestLuaUserData { }

    private sealed class TestScriptModule { }

    [Test]
    public void RegisterMethods_ShouldPopulateBootstrapRegistrations()
    {
        var registrations = new PluginBootstrapRegistrations();
        var context = new MoongatePluginContext("test-plugin", "/plugins/test-plugin", registrations);
        var descriptor = new PersistenceEntityDescriptor<HelpTicketEntity, Serial>(
            900,
            "test-help-ticket",
            1,
            static entity => entity.Id,
            static key => BitConverter.GetBytes((uint)key),
            static payload => (Serial)BitConverter.ToUInt32(payload)
        );

        context.RegisterService<IDisposable, MemoryStream>(42);
        context.RegisterPacketHandler<TestPacketHandler>();
        context.RegisterGameEventListener<TestGameEventListener>();
        context.RegisterConsoleCommand<TestCommand>();
        context.RegisterFileLoader<TestFileLoader>();
        context.RegisterLuaUserData<TestLuaUserData>();
        context.RegisterScriptModule<TestScriptModule>();
        context.RegisterPersistenceDescriptor(descriptor);

        Assert.Multiple(
            () =>
            {
                Assert.That(registrations.ServiceRegistrations, Has.Count.EqualTo(1));
                Assert.That(registrations.ServiceRegistrations[0].ServiceType, Is.EqualTo(typeof(IDisposable)));
                Assert.That(registrations.ServiceRegistrations[0].ImplementationType, Is.EqualTo(typeof(MemoryStream)));
                Assert.That(registrations.ServiceRegistrations[0].Priority, Is.EqualTo(42));
                Assert.That(registrations.PacketHandlerTypes, Is.EqualTo([typeof(TestPacketHandler)]));
                Assert.That(registrations.GameEventListenerTypes, Is.EqualTo([typeof(TestGameEventListener)]));
                Assert.That(registrations.ConsoleCommandTypes, Is.EqualTo([typeof(TestCommand)]));
                Assert.That(registrations.FileLoaderTypes, Is.EqualTo([typeof(TestFileLoader)]));
                Assert.That(registrations.LuaUserDataTypes, Is.EqualTo([typeof(TestLuaUserData)]));
                Assert.That(registrations.ScriptModuleTypes, Is.EqualTo([typeof(TestScriptModule)]));
                Assert.That(registrations.PersistenceDescriptorRegistrations, Has.Count.EqualTo(1));
            }
        );

        var registry = new PersistenceEntityRegistry();
        registrations.PersistenceDescriptorRegistrations[0](registry);

        Assert.That(registry.IsRegistered<HelpTicketEntity, Serial>(), Is.True);
    }
}
