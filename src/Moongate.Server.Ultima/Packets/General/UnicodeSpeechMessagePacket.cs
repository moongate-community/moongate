using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Speech;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     Sends Unicode speech or a system message to a client (0xAE).
/// </summary>
[PacketHandler(0xAE, PacketSizing.Variable, MinimumLength = 50)]
public sealed class UnicodeSpeechMessagePacket : BasePacket<UnicodeSpeechMessagePacket>, IOutgoingPacket
{
    private const int FixedLength = 48;
    private const int NameLength = 30;
    private static readonly Encoding StrictBigEndianUnicode = new UnicodeEncoding(true, false, true);

    public override int Length { get; }

    public Serial Serial { get; }

    public ushort Body { get; }

    public SpeechType Type { get; }

    public Hue Hue { get; }

    public SpeechFontType Font { get; }

    public string Language { get; }

    public string Name { get; }

    public string Text { get; }

    public UnicodeSpeechMessagePacket(
        Serial serial,
        ushort body,
        SpeechType type,
        Hue hue,
        SpeechFontType font,
        string language,
        string name,
        string text
    )
    {
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);

        if (language.Length != 3 || language.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("The language must contain three uppercase ASCII letters.", nameof(language));
        }

        if (name.Length > NameLength || name.Any(character => character is '\0' or > '\xFF'))
        {
            throw new ArgumentException("The name must fit 30 Latin-1 bytes without NUL.", nameof(name));
        }

        int textBytes;

        try
        {
            textBytes = StrictBigEndianUnicode.GetByteCount(text);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("The text must contain valid Unicode.", nameof(text), exception);
        }

        var length = (long)FixedLength + textBytes + 2;

        if (length > ushort.MaxValue)
        {
            throw new ArgumentException("The text exceeds the maximum packet length.", nameof(text));
        }

        Serial = serial;
        Body = body;
        Type = type;
        Hue = hue;
        Font = font;
        Language = language;
        Name = name;
        Text = text;
        Length = (int)length;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian(Body);
        writer.WriteByte((byte)Type);
        writer.WriteUInt16BigEndian(Hue.Value);
        writer.WriteUInt16BigEndian((ushort)Font);
        writer.WriteBytes(Encoding.ASCII.GetBytes(Language));
        writer.WriteByte(0);

        var nameBytes = new byte[NameLength];
        Encoding.Latin1.GetBytes(Name, nameBytes);
        writer.WriteBytes(nameBytes);
        writer.WriteBytes(StrictBigEndianUnicode.GetBytes(Text));
        writer.WriteUInt16BigEndian(0);
    }
}
