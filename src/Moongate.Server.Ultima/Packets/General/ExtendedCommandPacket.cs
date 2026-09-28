using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A general information subcommand, such as the client's language or screen size. (0xBF, variable). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xBF, PacketSizing.Variable, MinimumLength = 5, Description = "Extended command")]
public sealed class ExtendedCommandPacket : BasePacket<ExtendedCommandPacket>, IIncomingPacket<ExtendedCommandPacket>
{
    public override int Length { get; }

    private ExtendedCommandPacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ExtendedCommandPacket? packet)
    {
        packet = HasValidHeader(data) ? new ExtendedCommandPacket(data.Length) : null;

        return packet is not null;
    }
}
