using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PacketHandlerAttribute : Attribute
{
    public byte OpCode { get; }
    public PacketSizing Sizing { get; }
    public int Length { get; set; } = -1;
    public int MinimumLength { get; set; } = 3;
    public string? Description { get; set; }

    public PacketHandlerAttribute(byte opCode, PacketSizing sizing)
    {
        OpCode = opCode;
        Sizing = sizing;
    }
}
