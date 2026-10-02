using System.Diagnostics.CodeAnalysis;
using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Gumps;

/// <summary>
///     The client's answer to a gump (0xB1): the button pressed (0 when closed), the switches on, and the text of each
///     text entry.
/// </summary>
[PacketHandler(0xB1, PacketSizing.Variable, MinimumLength = 23, Description = "Gump response")]
public sealed class GumpResponsePacket : BasePacket<GumpResponsePacket>, IIncomingPacket<GumpResponsePacket>
{
    public override int Length { get; }

    public required uint Serial { get; init; }

    public required uint TypeId { get; init; }

    public required int ButtonId { get; init; }

    public required IReadOnlyList<int> Switches { get; init; }

    public required IReadOnlyList<(int Id, string Text)> TextEntries { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out GumpResponsePacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[3..]);

        if (!reader.TryReadUInt32BigEndian(out var serial) ||
            !reader.TryReadUInt32BigEndian(out var typeId) ||
            !reader.TryReadUInt32BigEndian(out var button) ||
            !reader.TryReadUInt32BigEndian(out var switchCount) ||
            switchCount > reader.Remaining / 4)
        {
            return false;
        }

        var switches = new List<int>((int)switchCount);

        for (var i = 0; i < switchCount; i++)
        {
            reader.TryReadUInt32BigEndian(out var id);
            switches.Add(unchecked((int)id));
        }

        if (!reader.TryReadUInt32BigEndian(out var textCount) || textCount > reader.Remaining / 4)
        {
            return false;
        }

        var texts = new List<(int, string)>((int)textCount);

        for (var i = 0; i < textCount; i++)
        {
            if (!reader.TryReadUInt16BigEndian(out var id) ||
                !reader.TryReadUInt16BigEndian(out var length) ||
                !reader.TryReadBytes(length * 2, out var text))
            {
                return false;
            }

            texts.Add((id, Encoding.BigEndianUnicode.GetString(text)));
        }

        packet = new()
        {
            Serial = serial, TypeId = typeId, ButtonId = unchecked((int)button), Switches = switches, TextEntries = texts
        };

        return true;
    }
}
