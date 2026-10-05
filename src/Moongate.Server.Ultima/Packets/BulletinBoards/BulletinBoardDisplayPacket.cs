using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Server.Ultima.Packets.BulletinBoards.Internal;

namespace Moongate.Server.Ultima.Packets.BulletinBoards;

/// <summary>
///     Opens a bulletin board on the client (0x71, sub-command 0x00): the board and its name, in a field of thirty
///     bytes. The messages follow as the content of a container (0x3C).
/// </summary>
[PacketHandler(0x71, PacketSizing.Variable, MinimumLength = TotalLength, Description = "Bulletin board: display")]
public sealed class BulletinBoardDisplayPacket : BasePacket<BulletinBoardDisplayPacket>, IOutgoingPacket
{
    private const byte Subcommand = 0x00;
    private const int TotalLength = 38;
    private const int NameLength = 30;

    private readonly byte[] _name;

    public override int Length => TotalLength;

    public Serial Board { get; }

    public BulletinBoardDisplayPacket(Serial board, string name)
    {
        Board = board;
        // One byte of the field stays zero: the name ends there.
        _name = BulletinBoardText.Cut(name, NameLength - 1);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian(TotalLength);
        writer.WriteByte(Subcommand);
        writer.WriteSerial(Board);
        writer.WriteBytes(_name);

        for (var index = _name.Length; index < NameLength; index++)
        {
            writer.WriteByte(0);
        }
    }
}
