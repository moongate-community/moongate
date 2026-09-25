using System.Diagnostics.CodeAnalysis;

namespace Moongate.Network.Packets.Interfaces;

/// <summary>
///     Parses one complete incoming packet from caller-owned bytes.
/// </summary>
/// <typeparam name="TSelf">
///     The concrete packet type.
/// </typeparam>
public interface IIncomingPacket<TSelf> : IPacket
    where TSelf : class, IIncomingPacket<TSelf>
{
    /// <summary>
    ///     Parses a complete frame into the concrete incoming packet type.
    /// </summary>
    /// <param name="data">
    ///     The complete packet bytes, including the opcode.
    /// </param>
    /// <param name="packet">
    ///     The parsed packet when parsing succeeds; otherwise <see langword="null" />.
    /// </param>
    /// <returns>
    ///     <see langword="true" /> when the frame is valid for this packet type.
    /// </returns>
    abstract static bool TryParse(
        ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out TSelf? packet
    );
}
