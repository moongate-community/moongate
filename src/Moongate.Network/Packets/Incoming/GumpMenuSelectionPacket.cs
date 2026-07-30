using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Gump response (0xB1): which button the player pressed, which switches were ticked, and what was
/// typed into each text field.
/// <para>
/// This record only parses. Nothing here is trusted: whether the button was ever drawn, and whether
/// the counts are plausible, is decided by the service that knows what it sent — this type has no
/// way to tell a genuine answer from a fabricated one.
/// </para>
/// </summary>
[PacketDocumentation(PacketFamilyType.Gumps, IsVariableLength = true)]
public readonly record struct GumpMenuSelectionPacket(
    uint Serial,
    int TypeId,
    int ButtonId,
    IReadOnlyList<int> Switches,
    IReadOnlyDictionary<int, string> TextEntries
) : IIncomingPacket<GumpMenuSelectionPacket>
{
    public static byte PacketId => 0xB1;

    public static GumpMenuSelectionPacket Read(ref SpanReader reader)
    {
        reader.ReadByte();   // packet id
        reader.ReadUInt16(); // length

        var serial = reader.ReadUInt32();
        var typeId = reader.ReadInt32();
        var buttonId = reader.ReadInt32();

        var switchCount = reader.ReadInt32();
        var switches = new List<int>(Math.Max(0, switchCount));

        for (var i = 0; i < switchCount && reader.Remaining >= 4; i++)
        {
            switches.Add(reader.ReadInt32());
        }

        var textCount = reader.ReadInt32();
        var textEntries = new Dictionary<int, string>(Math.Max(0, textCount));

        for (var i = 0; i < textCount && reader.Remaining >= 4; i++)
        {
            var entryId = reader.ReadUInt16();
            var length = reader.ReadUInt16();

            // Guard the read itself; whether the length is legitimate is the service's call.
            if (reader.Remaining < length * 2)
            {
                break;
            }

            textEntries[entryId] = reader.ReadBigUni(length);
        }

        return new(serial, typeId, buttonId, switches, textEntries);
    }
}
