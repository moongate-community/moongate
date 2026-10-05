using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A cliloc shown by the client in its language with a text of the server appended to it (0xCC), as ModernUO's
///     MessageLocalizedAffix: "Into your bank box I have placed a check in the amount of:" and the amount. The affix
///     is ASCII; the arguments of the cliloc, when it has placeholders, are big-endian UTF-16.
/// </summary>
[PacketHandler(0xCC, PacketSizing.Variable, MinimumLength = HeaderLength + 3, Description = "Localized message with affix")]
public sealed class LocalizedMessageAffixPacket : BasePacket<LocalizedMessageAffixPacket>, IOutgoingPacket
{
    private const int HeaderLength = 49;
    private const int NameLength = 30;
    private const byte RegularType = 0;
    private const byte AppendAffix = 0;
    private const ushort SpeechHue = 0x03B2;
    private const ushort SpeechFont = 3;

    public override int Length { get; }

    public Serial Serial { get; }

    public int Graphic { get; }

    public int Cliloc { get; }

    public string Name { get; }

    /// <summary>
    ///     Gets what the client writes after the text of the cliloc.
    /// </summary>
    public string Affix { get; }

    public string Arguments { get; }

    private LocalizedMessageAffixPacket(Serial serial, int graphic, int cliloc, string name, string affix, string arguments)
    {
        Serial = serial;
        Graphic = graphic;
        Cliloc = cliloc;
        Name = name.Length > NameLength ? name[..NameLength] : name;
        Affix = Ascii(affix);
        Arguments = arguments;
        Length = HeaderLength + Affix.Length + 1 + arguments.Length * 2 + 2;
    }

    /// <summary>
    ///     Gets a cliloc said by a mobile, overhead and in the journal under its name, with <paramref name="affix" />
    ///     appended.
    /// </summary>
    public static LocalizedMessageAffixPacket Spoken(
        Serial speaker,
        int body,
        int cliloc,
        string name,
        string affix,
        string arguments = ""
    )
    {
        return new(speaker, body, cliloc, name, affix, arguments);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)Graphic);
        writer.WriteByte(RegularType);
        writer.WriteUInt16BigEndian(SpeechHue);
        writer.WriteUInt16BigEndian(SpeechFont);
        writer.WriteUInt32BigEndian((uint)Cliloc);
        writer.WriteByte(AppendAffix);
        writer.WriteFixedAscii(Ascii(Name), NameLength);
        writer.WriteBytes(Encoding.ASCII.GetBytes(Affix));
        writer.WriteByte(0);
        writer.WriteBytes(Encoding.BigEndianUnicode.GetBytes(Arguments));
        writer.WriteUInt16BigEndian(0);
    }

    // The wire is ASCII here: any other character, and a zero that would end the text early, becomes a question mark.
    private static string Ascii(string text)
    {
        return new(text.Select(letter => letter is > '\0' and <= '\x7F' ? letter : '?').ToArray());
    }
}
