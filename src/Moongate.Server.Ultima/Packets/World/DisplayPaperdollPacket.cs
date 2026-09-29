using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.World;

/// <summary>
///     Opens a mobile's paperdoll (0x88), as ModernUO sends it: the mobile, its title in 60 bytes, and flags for war
///     mode (0x01) and whether the viewer may take items off it (0x02).
/// </summary>
[PacketHandler(0x88, PacketSizing.Fixed, Length = 66)]
public sealed class DisplayPaperdollPacket : BaseFixedPacket<DisplayPaperdollPacket>, IOutgoingPacket
{
    private const int TitleLength = 60;

    public Serial Mobile { get; }

    public string Title { get; }

    public bool WarMode { get; }

    public bool CanLift { get; }

    public DisplayPaperdollPacket(Serial mobile, string title, bool warMode, bool canLift)
    {
        Mobile = mobile;
        Title = ToAscii(title);
        WarMode = warMode;
        CanLift = canLift;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteSerial(Mobile);
        writer.WriteFixedAscii(Title, TitleLength);
        writer.WriteByte((byte)((WarMode ? 0x01 : 0) | (CanLift ? 0x02 : 0)));
    }

    // The client shows 60 bytes; a longer title is cut, and a character outside ASCII becomes '?'.
    private static string ToAscii(string title)
    {
        var cut = title.Length > TitleLength ? title[..TitleLength] : title;

        return string.Create(cut.Length, cut, static (span, source) =>
            {
                for (var i = 0; i < source.Length; i++)
                {
                    span[i] = char.IsAscii(source[i]) ? source[i] : '?';
                }
            }
        );
    }
}
