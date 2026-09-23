namespace Moongate.Network.Packets.Interfaces;

/// <summary>
/// Describes one complete Ultima Online protocol packet.
/// </summary>
public interface IPacket
{
    /// <summary>Gets the opcode that identifies the packet on the wire.</summary>
    byte OpCode { get; }

    /// <summary>Gets the complete encoded packet length in bytes.</summary>
    int Length { get; }
}
