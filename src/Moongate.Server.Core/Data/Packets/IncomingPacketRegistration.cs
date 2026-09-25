using Moongate.Network.Packets.Registry;

namespace Moongate.Server.Core.Data.Packets;

/// <summary>
///     An incoming packet a plugin adds to the server's packet registry, applied when the registry is built at
///     startup, after every plugin has registered.
/// </summary>
public sealed record IncomingPacketRegistration(Type PacketType, Action<PacketRegistry> Register);
