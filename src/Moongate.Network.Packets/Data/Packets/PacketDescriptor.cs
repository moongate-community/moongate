using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Data.Packets;

/// <summary>
/// Represents struct.
/// </summary>
public readonly record struct PacketDescriptor(
    byte OpCode,
    PacketSizing Sizing,
    int Length,
    string Description,
    Type HandlerType
);
