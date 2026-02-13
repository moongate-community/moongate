using Moongate.Core.Server.Packets;
using Moongate.Core.Spans;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Packets.Chat;

public class UnicodeSpeechRequestPacket : BaseUoPacket
{
    public UnicodeSpeechRequestPacket() : base(0xAD) { }

    public ChatMessageType MessageType { get; set; }

    public short Hue { get; set; }

    public int[] Keywords { get; set; }

    public string Language { get; set; }

    public string Text { get; set; }

    protected override bool Read(SpanReader reader)
    {
        reader.ReadInt16(); // length
        MessageType = (ChatMessageType)reader.ReadByte();
        Hue = reader.ReadInt16();
        reader.ReadInt16(); // font
        Language = reader.ReadAscii(4);
        string text;

        var isEncoded = (MessageType & ChatMessageType.Encoded) != 0;

        if (isEncoded)
        {
            int value = reader.ReadInt16();
            var count = (value & 0xFFF0) >> 4;
            var hold = value & 0xF;

            if (count is < 0 or > 50)
            {
                return false;
            }

            var keyList = new KeywordList();

            for (var i = 0; i < count; ++i)
            {
                int speechID;

                if ((i & 1) == 0)
                {
                    hold <<= 8;
                    hold |= reader.ReadByte();
                    speechID = hold;
                    hold = 0;
                }
                else
                {
                    value = reader.ReadInt16();
                    speechID = (value & 0xFFF0) >> 4;
                    hold = value & 0xF;
                }

                if (!keyList.Contains(speechID))
                {
                    keyList.Add(speechID);
                }
            }

            text = reader.ReadUTF8Safe();

            Keywords = keyList.ToArray();
        }
        else
        {
            text = reader.ReadBigUniSafe();

            Keywords = [];
        }

        text = text.Trim();

        if (text.Length is <= 0 or > 128)
        {
            return false;
        }

        MessageType &= ~ChatMessageType.Encoded;

        if (!Enum.IsDefined(MessageType))
        {
            MessageType = ChatMessageType.Regular;
        }

        Text = text;

        return true;
    }
}
