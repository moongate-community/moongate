using DryIoc;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Registry;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Bootstrap.Internal;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Packets.Characters;

namespace Moongate.Tests.Server.Bootstrap.Internal;

public sealed class PacketRegistryFactoryTests
{
    [Fact]
    public void Create_WithoutPluginPackets_HasTheBuiltInPacketsAndIsFrozen()
    {
        using var container = new Container();
        PacketRegistryFactory.Register(container);

        var registry = container.Resolve<PacketRegistry>();

        Assert.True(registry.IsFrozen);
        Assert.Equal(PacketRegistry.Default.RegisteredPackets.Count, registry.RegisteredPackets.Count);
        Assert.True(registry.TryGetDescriptor(0x91, PacketDirection.Incoming, out var gameLogin));
        Assert.Equal(typeof(GameLoginPacket), gameLogin.PacketType);
        Assert.False(registry.TryGetDescriptor(0x8D, PacketDirection.Incoming, out _));
    }

    [Fact]
    public void Create_WithAPluginPacket_AddsItToTheBuiltInOnes()
    {
        using var container = new Container();
        container.RegisterIncomingPacket<CreateCharacterEnhancedPacket>();
        PacketRegistryFactory.Register(container);

        var registry = container.Resolve<PacketRegistry>();

        Assert.True(registry.TryGetDescriptor(0x8D, PacketDirection.Incoming, out var descriptor));
        Assert.Equal(typeof(CreateCharacterEnhancedPacket), descriptor.PacketType);
        Assert.Equal(PacketRegistry.Default.RegisteredPackets.Count + 1, registry.RegisteredPackets.Count);
        Assert.False(PacketRegistry.Default.TryGetDescriptor(0x8D, PacketDirection.Incoming, out _));
    }

    [Fact]
    public void Register_Twice_KeepsOneSingleton()
    {
        using var container = new Container();
        PacketRegistryFactory.Register(container);
        PacketRegistryFactory.Register(container);

        Assert.Same(container.Resolve<PacketRegistry>(), container.Resolve<PacketRegistry>());
    }
}
