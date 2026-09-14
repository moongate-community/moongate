namespace Moongate.Network.Packets.Interfaces;

/// <summary>
/// Describes one complete Ultima Online protocol packet.
/// </summary>
public interface IPacket
{
    byte OpCode { get; }
    int Length { get; }
}
