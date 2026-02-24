using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;

namespace Moongate.Network.Packets.Outgoing.UI;

/// <summary>
/// Sends a generic (uncompressed) gump layout and string table.
/// </summary>
[PacketHandler(0xB0, PacketSizing.Variable, Description = "Send Gump Menu Dialog")]
public sealed class GenericGumpPacket : BaseGameNetworkPacket
{
    public uint SenderSerial { get; set; }

    public uint GumpId { get; set; }

    public uint X { get; set; }

    public uint Y { get; set; }

    public string Layout { get; set; } = string.Empty;

    public List<string> TextLines { get; } = [];

    public GenericGumpPacket()
        : base(0xB0) { }

    public override void Write(ref SpanWriter writer)
    {
        var layoutBytes = BuildLayoutBytes(Layout);

        if (layoutBytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Generic gump layout exceeds ushort length.");
        }

        if (TextLines.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException("Generic gump text line count exceeds ushort length.");
        }

        writer.Write(OpCode);
        writer.Write((ushort)0);
        writer.Write(SenderSerial);
        writer.Write(GumpId);
        writer.Write(X);
        writer.Write(Y);
        writer.Write((ushort)layoutBytes.Length);
        writer.Write(layoutBytes);
        writer.Write((ushort)TextLines.Count);

        foreach (var line in TextLines)
        {
            var text = line ?? string.Empty;

            if (text.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("Generic gump text line exceeds ushort length.");
            }

            writer.Write((ushort)text.Length);
            writer.WriteBigUni(text, text.Length);
        }

        writer.WritePacketLength();
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 22)
        {
            return false;
        }

        var declaredLength = reader.ReadUInt16();

        if (declaredLength != reader.Length)
        {
            return false;
        }

        SenderSerial = reader.ReadUInt32();
        GumpId = reader.ReadUInt32();
        X = reader.ReadUInt32();
        Y = reader.ReadUInt32();

        var commandSectionLength = reader.ReadUInt16();

        if (commandSectionLength > reader.Remaining)
        {
            return false;
        }

        var commandSection = reader.ReadBytes(commandSectionLength);
        Layout = DecodeLayout(commandSection);

        if (reader.Remaining < 2)
        {
            return false;
        }

        var textLineCount = reader.ReadUInt16();
        TextLines.Clear();

        for (var i = 0; i < textLineCount; i++)
        {
            if (reader.Remaining < 2)
            {
                return false;
            }

            var charLength = reader.ReadUInt16();
            var bytesLength = checked(charLength * 2);

            if (bytesLength > reader.Remaining)
            {
                return false;
            }

            var textBytes = reader.ReadBytes(bytesLength);
            TextLines.Add(Encoding.BigEndianUnicode.GetString(textBytes));
        }

        return reader.Remaining == 0;
    }

    private static byte[] BuildLayoutBytes(string layout)
    {
        var text = layout ?? string.Empty;

        if (text.Length > 0 && text[^1] == '\0')
        {
            return Encoding.ASCII.GetBytes(text);
        }

        var bytes = Encoding.ASCII.GetBytes(text);
        var withTerminator = new byte[bytes.Length + 1];
        bytes.CopyTo(withTerminator, 0);

        return withTerminator;
    }

    private static string DecodeLayout(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var terminatorIndex = bytes.IndexOf((byte)0);
        var content = terminatorIndex >= 0 ? bytes[..terminatorIndex] : bytes;

        return Encoding.ASCII.GetString(content);
    }
}
