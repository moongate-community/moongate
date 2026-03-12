using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Network.Spans;
namespace Moongate.Network.Packets.Incoming.Books;

[PacketHandler(0x93, PacketSizing.Fixed, Length = 99, Description = "Book Header ( Old )")]

/// <summary>
/// Represents BookHeaderOldPacket.
/// </summary>
public class BookHeaderOldPacket : BaseGameNetworkPacket
{
    public uint BookSerial { get; set; }

    public bool IsWritable { get; set; }

    public ushort PageCount { get; set; }

    public string Title { get; set; }

    public string Author { get; set; }

    public BookHeaderOldPacket()
        : base(0x93, 99)
    {
        Title = string.Empty;
        Author = string.Empty;
    }

    public override void Write(ref SpanWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(BookSerial);
        writer.Write(false);
        writer.Write(IsWritable);
        writer.Write(PageCount);
        writer.WriteAscii(Title, 60);
        writer.WriteAscii(Author, 30);
    }

    protected override bool ParsePayload(ref SpanReader reader)
    {
        if (reader.Remaining < 98)
        {
            return false;
        }

        BookSerial = reader.ReadUInt32();
        _ = reader.ReadBoolean();
        IsWritable = reader.ReadBoolean();
        PageCount = reader.ReadUInt16();
        Title = reader.ReadAscii(60);
        Author = reader.ReadAscii(30);

        return reader.Remaining == 0;
    }
}
