using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     An extended command (0xBF): a subcommand number and its own data, read by the handler of that subcommand.
/// </summary>
[PacketHandler(0xBF, PacketSizing.Variable, MinimumLength = 5, Description = "Extended command")]
public sealed class ExtendedCommandPacket : BasePacket<ExtendedCommandPacket>, IIncomingPacket<ExtendedCommandPacket>
{
    private const int HeaderLength = 5;

    public override int Length { get; }

    public ushort Subcommand { get; }

    public byte[] Payload { get; }

    private ExtendedCommandPacket(int length, ushort subcommand, byte[] payload)
    {
        Length = length;
        Subcommand = subcommand;
        Payload = payload;
    }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ExtendedCommandPacket? packet)
    {
        packet = HasValidHeader(data) && data.Length >= HeaderLength
                     ? new ExtendedCommandPacket(data.Length, (ushort)(data[3] << 8 | data[4]), data[HeaderLength..].ToArray())
                     : null;

        return packet is not null;
    }
}
