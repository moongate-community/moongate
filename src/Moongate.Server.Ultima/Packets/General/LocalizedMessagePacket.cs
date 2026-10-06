using System.Text;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.General;

/// <summary>
///     A cliloc shown by the client in its language (0xC1), as ModernUO's localized message: here the label a single
///     click shows over an object (type 6, hue 0x3B2, font 3), with the arguments in little-endian UTF-16.
/// </summary>
[PacketHandler(0xC1, PacketSizing.Variable, MinimumLength = HeaderLength + 2)]
public sealed class LocalizedMessagePacket : BasePacket<LocalizedMessagePacket>, IOutgoingPacket
{
    private const int HeaderLength = 48;
    private const int NameLength = 30;
    private const byte LabelType = 6;
    private const byte RegularType = 0;
    private const ushort LabelHue = 0x03B2;
    private const ushort LabelFont = 3;
    private const int NoGraphic = 0xFFFF;
    private const string SystemName = "System";

    private static readonly Serial NoSerial = new(0xFFFFFFFF);

    private readonly byte _type = LabelType;

    public override int Length { get; }

    public Serial Serial { get; }

    public int Graphic { get; }

    public int Cliloc { get; }

    public string Name { get; }

    public string Arguments { get; }

    /// <summary>
    ///     Gets the colour of the text; the usual grey of a label unless a system message asks for another.
    /// </summary>
    public int Hue { get; private init; } = LabelHue;

    public LocalizedMessagePacket(Serial serial, int graphic, int cliloc, string name, string arguments)
    {
        Serial = serial;
        Graphic = graphic;
        Cliloc = cliloc;
        Name = name.Length > NameLength ? name[..NameLength] : name;
        Arguments = arguments;
        Length = HeaderLength + arguments.Length * 2 + 2;
    }

    /// <summary>
    ///     Gets a cliloc shown as a system message, in the lower left of the screen, as ModernUO's
    ///     SendLocalizedMessage: no object, the name "System".
    /// </summary>
    public static LocalizedMessagePacket System(int cliloc, string arguments = "", int? hue = null)
    {
        return new(NoSerial, NoGraphic, cliloc, SystemName, arguments, RegularType) { Hue = hue ?? LabelHue };
    }

    /// <summary>
    ///     Gets a cliloc said by a mobile, overhead and in the journal under its name, as ModernUO's localized Say:
    ///     each client shows it in its own language.
    /// </summary>
    public static LocalizedMessagePacket Spoken(Serial speaker, int body, int cliloc, string name, string arguments = "")
    {
        return new(speaker, body, cliloc, name, arguments, RegularType);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteSerial(Serial);
        writer.WriteUInt16BigEndian((ushort)Graphic);
        writer.WriteByte(_type);
        writer.WriteUInt16BigEndian((ushort)Hue);
        writer.WriteUInt16BigEndian(LabelFont);
        writer.WriteUInt32BigEndian((uint)Cliloc);
        writer.WriteFixedAscii(new string(Name.Select(c => c is > '\0' and <= '\x7F' ? c : '?').ToArray()), NameLength);
        writer.WriteBytes(Encoding.Unicode.GetBytes(Arguments));
        writer.WriteUInt16BigEndian(0);
    }

    private LocalizedMessagePacket(Serial serial, int graphic, int cliloc, string name, string arguments, byte type)
        : this(serial, graphic, cliloc, name, arguments)
    {
        _type = type;
    }
}
