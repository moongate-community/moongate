using DryIoc;
using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Packets;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Sessions;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Packets;

namespace Moongate.Tests.Server.Core.Packets;

public sealed class PacketHandlerRegistryTests
{
    [Fact]
    public async Task RegisterAsync_BindsSingletonAndRejectsDuplicateSyncRegistration()
    {
        using var container = new Container();
        container.RegisterAsyncPacketHandler<PingPacket, AsyncPingPacketHandler>();
        var registration = container.Resolve<PacketHandlerRegistry>().Freeze()[typeof(PingPacket)];
        Assert.True(registration.IsAsync);
        Assert.Same(container.Resolve<AsyncPingPacketHandler>(), container.Resolve<AsyncPingPacketHandler>());
        await registration.BindAsync(container)(null!, new PingPacket(42), CancellationToken.None);
        Assert.Equal((byte)42, container.Resolve<AsyncPingPacketHandler>().LastSequence);
        Assert.Throws<InvalidOperationException>(
            () => container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>()
        );
        Assert.False(container.IsRegistered<RecordingPacketHandler>());
    }

    [Fact]
    public void RegisterSync_RejectsDuplicateAsyncRegistrationBeforeContainerMutation()
    {
        using var container = new Container();
        container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>();
        Assert.Throws<InvalidOperationException>(
            () => container.RegisterAsyncPacketHandler<PingPacket, AsyncPingPacketHandler>()
        );
        Assert.False(container.IsRegistered<AsyncPingPacketHandler>());
    }

    [Fact]
    public void RegisterAsync_RejectsTransientAndFrozenRegistry()
    {
        using var container = new Container();
        container.Register<AsyncPingPacketHandler>(Reuse.Transient);
        Assert.Throws<InvalidOperationException>(
            () => container.RegisterAsyncPacketHandler<PingPacket, AsyncPingPacketHandler>()
        );
        Assert.Empty(container.Resolve<PacketHandlerRegistry>().Registrations);

        using var frozenContainer = new Container();
        frozenContainer.RegisterInstance(new PacketHandlerRegistry());
        frozenContainer.Resolve<PacketHandlerRegistry>().Freeze();
        Assert.Throws<InvalidOperationException>(
            () => frozenContainer.RegisterAsyncPacketHandler<PingPacket, AsyncPingPacketHandler>()
        );
        Assert.False(frozenContainer.IsRegistered<AsyncPingPacketHandler>());
    }

    [Fact]
    public async Task Freeze_DistinctPacketTypesBindToSameSingletonWithTypedDelegates()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        var session = new SessionService(fixture.Loop).GetOrCreate(fixture.Client);
        using var container = new Container();
        container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>();
        container.RegisterPacketHandler<ClientVersionPacket, RecordingPacketHandler>();
        var registrations = container.Resolve<PacketHandlerRegistry>().Freeze();
        var handler = container.Resolve<RecordingPacketHandler>();
        byte sequence = 0;
        handler.OnPing = (_, value) => sequence = value;
        registrations[typeof(PingPacket)].Bind(container)(session, new PingPacket(42));
        registrations[typeof(ClientVersionPacket)].Bind(container)(session, new ClientVersionPacket("7.0"));
        Assert.Equal((byte)42, sequence);
        Assert.Equal("7.0", handler.Version);
        Assert.Same(handler, container.Resolve<RecordingPacketHandler>());
        Assert.Equal(2, registrations.Count);
    }

    [Fact]
    public void Register_AfterFreezeRejectsBeforeContainerMutation()
    {
        using var container = new Container();
        container.RegisterInstance(new PacketHandlerRegistry());
        var frozen = container.Resolve<PacketHandlerRegistry>().Freeze();
        Assert.Throws<InvalidOperationException>(
            () => container.RegisterPacketHandler<PingPacket, DependentPacketHandler>()
        );
        Assert.Empty(frozen);
        Assert.False(container.IsRegistered<DependentPacketHandler>());
    }

    [Fact]
    public void Register_DuplicateRejectsBeforeContainerMutation()
    {
        using var container = new Container();
        container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>();
        Assert.Throws<InvalidOperationException>(
            () => container.RegisterPacketHandler<PingPacket, DependentPacketHandler>()
        );
        Assert.Single(container.Resolve<PacketHandlerRegistry>().Registrations);
        Assert.False(container.IsRegistered<DependentPacketHandler>());
    }

    [Fact]
    public void Register_PreRegisteredSingletonPreservesInstanceAcrossPacketTypes()
    {
        using var container = new Container();
        var handler = new RecordingPacketHandler();
        container.RegisterInstance(handler);
        container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>();
        container.RegisterPacketHandler<ClientVersionPacket, RecordingPacketHandler>();
        Assert.Same(handler, container.Resolve<RecordingPacketHandler>());
        Assert.Equal(2, container.Resolve<PacketHandlerRegistry>().Registrations.Count);
    }

    [Fact]
    public void Register_PreRegisteredTransientRejectsWithoutPartialMetadata()
    {
        using var container = new Container();
        container.Register<RecordingPacketHandler>(Reuse.Transient);
        Assert.Throws<InvalidOperationException>(
            () => container.RegisterPacketHandler<PingPacket, RecordingPacketHandler>()
        );
        Assert.Empty(container.Resolve<PacketHandlerRegistry>().Registrations);
        Assert.NotSame(container.Resolve<RecordingPacketHandler>(), container.Resolve<RecordingPacketHandler>());
    }

    [Fact]
    public async Task Start_MissingHandlerDependencyFailsStartup()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        container.RegisterPacketHandler<PingPacket, DependentPacketHandler>();
        var dispatcher = new PacketDispatchService(
            fixture.Loop,
            new SessionService(fixture.Loop),
            container.Resolve<PacketHandlerRegistry>(),
            container
        );
        await Assert.ThrowsAnyAsync<Exception>(() => dispatcher.StartAsync());
    }

    [Fact]
    public async Task Start_ResolvesDeferredDependenciesOnlyAfterPluginRegistration()
    {
        await using var fixture = await SessionFixture.CreateAsync();
        using var container = new Container();
        container.RegisterPacketHandler<PingPacket, DependentPacketHandler>();
        var dispatcher = new PacketDispatchService(
            fixture.Loop,
            new SessionService(fixture.Loop),
            container.Resolve<PacketHandlerRegistry>(),
            container
        );
        container.Register<RecordingPacketHandler>(Reuse.Singleton);
        await dispatcher.StartAsync();
        Assert.Same(container.Resolve<DependentPacketHandler>(), container.Resolve<DependentPacketHandler>());
        await dispatcher.StopAsync();
    }
}
