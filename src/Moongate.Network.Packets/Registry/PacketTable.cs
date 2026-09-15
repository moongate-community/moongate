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
        registry.RegisterIncoming<PingPacket>();
        registry.RegisterIncoming<LoginSeedPacket>();
        registry.RegisterIncoming<AccountLoginPacket>();
        registry.RegisterIncoming<GameLoginPacket>();
        registry.RegisterIncoming<ServerSelectPacket>();
        registry.RegisterIncoming<ClientVersionPacket>();
        registry.RegisterOutgoing<LoginDeniedPacket>();
        registry.RegisterOutgoing<LoginCompletePacket>();
        registry.RegisterOutgoing<ClientVersionRequestPacket>();
        registry.RegisterOutgoing<ServerListPacket>();
        registry.RegisterOutgoing<ServerRedirectPacket>();
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
