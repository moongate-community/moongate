using Moongate.Server.Data.Session;

namespace Moongate.Server.Data;

/// <summary>Ambient context handed to a packet handler: the session the packet arrived on.</summary>
public readonly record struct PacketContext(PlayerSession Session);
