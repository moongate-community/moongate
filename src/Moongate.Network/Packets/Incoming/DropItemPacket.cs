using Moongate.Core.Primitives;
using Moongate.Network.Attributes;
using Moongate.Network.Interfaces;
using Moongate.Network.Types;
using SquidStd.Network.Spans;

namespace Moongate.Network.Packets.Incoming;

/// <summary>
/// Drop item (0x08): where the client wants to put the item it is holding. A <c>Container</c> of
/// 0xFFFFFFFF means the ground at X/Y/Z; anything else is a container, and then X/Y are the position
/// inside its gump. 15 bytes fixed.
/// </summary>
[PacketDocumentation(PacketFamilyType.ItemsContainers, Length = 15)]
public readonly record struct DropItemPacket(
    Serial Serial,
    ushort X,
    ushort Y,
    sbyte Z,
    byte GridIndex,
    Serial Container
) : IIncomingPacket<DropItemPacket>
{
    /// <summary>The container serial modern clients send to mean "drop this on the ground".</summary>
    public const uint GroundContainer = 0xFFFFFFFF;

    public static byte PacketId => 0x08;

    public static DropItemPacket Read(ref SpanReader reader)
    {
        reader.ReadByte(); // packet id

        return new(
            new Serial(reader.ReadUInt32()),
            reader.ReadUInt16(),
            reader.ReadUInt16(),
            reader.ReadSByte(),
            reader.ReadByte(), // grid index: a slot hint we do not use
            new Serial(reader.ReadUInt32())
        );
    }
}
