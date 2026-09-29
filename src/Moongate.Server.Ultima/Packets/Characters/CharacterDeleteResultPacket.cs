using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Types.Characters;

namespace Moongate.Server.Ultima.Packets.Characters;

/// <summary>
///     Tells the client why the character it asked to delete (0x83) was not deleted.
/// </summary>
[PacketHandler(0x85, PacketSizing.Fixed, Length = 2)]
public sealed class CharacterDeleteResultPacket : BaseFixedPacket<CharacterDeleteResultPacket>, IOutgoingPacket
{
    public CharacterDeleteResultType Result { get; }

    public CharacterDeleteResultPacket(CharacterDeleteResultType result)
    {
        Result = result;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteByte((byte)Result);
    }
}
