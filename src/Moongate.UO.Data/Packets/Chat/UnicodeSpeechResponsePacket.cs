using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Chat;

/// <summary>
/// Packet 0xAE - Unicode Speech Message (Server -> Client)
/// Sends speech/text messages to the client
/// </summary>
public class UnicodeSpeechResponsePacket : BaseUoPacket
{
    public UnicodeSpeechResponsePacket() : base(0xAE)
    {
    }


    public byte AsciiOpCode => 0x1C;

    public bool IsUnicode { get; set; } = true;

    public Serial Serial { get; set; }

    public string Language { get; set; } = "ENU";

    public string Name { get; set; }

    public string Text { get; set; }

    public int Font { get; set; }

    public ChatMessageType MessageType { get; set; }

    public int Graphic { get; set; }

    public int Hue { get; set; } = 0;

    protected override bool Read(SpanReader reader)
    {
        return true;
    }

    public override ReadOnlyMemory<byte> Write(SpanWriter writer)
    {
        writer.Write(IsUnicode ? OpCode : AsciiOpCode);
        writer.Seek(2, SeekOrigin.Current);
        writer.Write(Serial.Value);
        writer.Write((short)Graphic);
        writer.Write((byte)MessageType);
        writer.Write((short)Hue);
        writer.Write((short)Font);


        if (Hue == 0)
        {
            Hue = 0x3B2;
        }

        if (!IsUnicode)
        {
            writer.WriteAscii(Name, 30);
            writer.WriteAsciiNull(Text);
        }
        else
        {
            writer.WriteAscii(Language, 4);
            writer.WriteAscii(Name, 30);
            writer.WriteBigUniNull(Text);
        }

        writer.WritePacketLength();

        return writer.ToArray();
    }
}
