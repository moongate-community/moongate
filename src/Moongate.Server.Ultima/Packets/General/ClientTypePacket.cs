using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The kind of client that connected. (0xE1, variable). Only its frame is read: the server does not act on it yet.
/// </summary>
[PacketHandler(0xE1, PacketSizing.Variable, MinimumLength = 3, Description = "Client type")]
public sealed class ClientTypePacket : BasePacket<ClientTypePacket>, IIncomingPacket<ClientTypePacket>
{
    public override int Length { get; }

    private ClientTypePacket(int length)
    {
        Length = length;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ClientTypePacket? packet)
    {
        packet = HasValidHeader(data) ? new ClientTypePacket(data.Length) : null;

        return packet is not null;
    }
}
