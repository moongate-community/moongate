using System.Diagnostics.CodeAnalysis;

namespace Moongate.Network.Packets.Interfaces;

/// <summary>
/// Parses one complete incoming packet from caller-owned bytes.
/// </summary>
/// <typeparam name="TSelf">The concrete packet type.</typeparam>
public interface IIncomingPacket<TSelf> : IPacket
    where TSelf : class, IIncomingPacket<TSelf>
{
    abstract static bool TryParse(
        ReadOnlySpan<byte> data,
        [NotNullWhen(true)] out TSelf? packet
    );
}
