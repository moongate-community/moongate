namespace Moongate.Server.Attributes;

/// <summary>
/// Declares the packet opcodes handled by a packet listener type.
/// This attribute is consumed by the packet-listener source generator.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RegisterPacketHandlerAttribute : Attribute
{
    /// <summary>
    /// Gets the packet opcode bound to the annotated listener.
    /// </summary>
    public byte OpCode { get; }

    /// <summary>
    /// Initializes a new attribute instance for the specified opcode.
    /// </summary>
    /// <param name="opCode">Packet opcode handled by the listener.</param>
    public RegisterPacketHandlerAttribute(byte opCode)
    {
        OpCode = opCode;
    }
}
