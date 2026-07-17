using Moongate.Server.Abstractions.Data.Session;

namespace Moongate.Server.Abstractions.Data;

/// <summary>Ambient context handed to a packet handler: the session the packet arrived on.</summary>
public readonly record struct PacketContext(PlayerSession Session);
