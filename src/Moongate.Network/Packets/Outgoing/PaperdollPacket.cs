using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Outgoing;

/// <summary>
/// Open paperdoll (0x88): tells the client to open the character window for a mobile. 66 bytes fixed.
/// The text is the label shown on the window; <c>CanLift</c> lets the viewer drag items off the
/// paperdoll, which is why it is only set for your own character.
/// </summary>
[PacketDocumentation(PacketFamilyType.StatusSkills)]
public readonly record struct PaperdollPacket(Serial Serial, string Text, bool Warmode, bool CanLift)
    : IOutgoingPacket
{
    public const byte PacketId = 0x88;

    private const int TextLength = 60;
    private const byte WarmodeFlag = 0x01;
    private const byte CanLiftFlag = 0x02;

    public void Write(ref SpanWriter writer)
    {
        byte flags = 0x00;

        if (Warmode)
        {
            flags |= WarmodeFlag;
        }

        if (CanLift)
        {
            flags |= CanLiftFlag;
        }

        writer.Write(PacketId);
        writer.Write(Serial);
        writer.WriteAscii(Text, TextLength);
        writer.Write(flags);
    }
}
