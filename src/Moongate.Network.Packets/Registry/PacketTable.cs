using Moongate.Network.Packets.General;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Outgoing.Login;

namespace Moongate.Network.Packets.Registry;

public static class PacketTable
{
    /// <summary>
    /// Registers the built-in packet types in an empty mutable registry.
    /// </summary>
    /// <param name="registry">The registry to configure before use.</param>
    public static void Register(PacketRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.RegisterPacket<PingPacket>();
        registry.RegisterPacket<LoginSeedPacket>();
        registry.RegisterPacket<AccountLoginPacket>();
        registry.RegisterPacket<GameLoginPacket>();
        registry.RegisterPacket<ServerSelectPacket>();
        registry.RegisterPacket<ClientVersionPacket>();
        registry.RegisterPacket<LoginDeniedPacket>();
        registry.RegisterPacket<LoginCompletePacket>();
        registry.RegisterPacket<ClientVersionRequestPacket>();
        registry.RegisterPacket<ServerListPacket>();
        registry.RegisterPacket<ServerRedirectPacket>();
        registry.RegisterPacket<SupportFeaturesPacket>();
    }

    /// <summary>
    /// Creates and freezes a registry containing all built-in packet types.
    /// </summary>
    /// <returns>A ready-to-use frozen registry.</returns>
    public static PacketRegistry CreateRegistry()
    {
        var registry = new PacketRegistry();
        Register(registry);
        registry.Freeze();
        return registry;
    }
}
