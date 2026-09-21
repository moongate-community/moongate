using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Data.Packets;

public sealed class PacketDescriptor
{
    public byte OpCode { get; }
    public PacketSizing Sizing { get; }
    public int? FixedLength { get; }
    public int MinimumLength { get; }
    public PacketDirection Direction { get; }
    public Type PacketType { get; }
    public string? Description { get; }

    internal PacketDescriptor(
        byte opCode,
        PacketSizing sizing,
        int? fixedLength,
        int minimumLength,
        PacketDirection direction,
        Type packetType,
        string? description
    )
    {
        OpCode = opCode;
        Sizing = sizing;
        FixedLength = fixedLength;
        MinimumLength = minimumLength;
        Direction = direction;
        PacketType = packetType;
        Description = description;
    }
}
