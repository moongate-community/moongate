using SquidStd.Network.Spans;

namespace Moongate.Network.Interfaces;

/// <summary>
/// A packet the server decodes from the wire. Implemented by incoming packet records so the
/// dispatch layer can read them generically by opcode.
/// </summary>
/// <typeparam name="TSelf">The implementing packet type.</typeparam>
public interface IIncomingPacket<TSelf> where TSelf : IIncomingPacket<TSelf>
{
    /// <summary>The opcode (first byte) that identifies this packet.</summary>
    abstract static byte PacketId { get; }

    /// <summary>Reads the whole packet, id-first, from the reader.</summary>
    abstract static TSelf Read(ref SpanReader reader);
}
