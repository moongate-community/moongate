using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     The client's answer to a hue picker (0x95): the id of the picker and the hue the player picked. The graphic
///     between them is not read, as in ModernUO.
/// </summary>
[PacketHandler(0x95, PacketSizing.Fixed, Length = 9, Description = "Hue picker response")]
public sealed class HuePickerResponsePacket
    : BaseFixedPacket<HuePickerResponsePacket>, IIncomingPacket<HuePickerResponsePacket>
{
    public required int PickerId { get; init; }

    /// <summary>
    ///     Gets the hue as the client sent it, not yet kept in the range a player may pick.
    /// </summary>
    public required int Hue { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out HuePickerResponsePacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadUInt32BigEndian(out var pickerId) ||
            !reader.TryReadUInt16BigEndian(out _) ||
            !reader.TryReadUInt16BigEndian(out var hue))
        {
            return false;
        }

        packet = new() { PickerId = unchecked((int)pickerId), Hue = hue };

        return true;
    }
}
