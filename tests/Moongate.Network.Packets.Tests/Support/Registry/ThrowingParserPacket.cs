using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.Tests.Support.Registry;

[PacketHandler(0xD0, PacketSizing.Fixed, Length = 2)]
public sealed class ThrowingParserPacket : BaseFixedPacket<ThrowingParserPacket>, IIncomingPacket<ThrowingParserPacket>
{
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ThrowingParserPacket? packet)
        => throw new InvalidOperationException("The parser must not be called for invalid framing.");
}
