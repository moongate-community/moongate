using Moongate.Core.Primitives;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.ContextMenus;

/// <summary>
///     Shows the context menu of a mobile or an item (0xBF, sub-command 0x14): each entry is a text of the client,
///     and the client answers with the index of the one chosen.
/// </summary>
/// <remarks>
///     ModernUO's <c>SendDisplayContextMenu</c> in the format every client from 6.0.0.0 takes: format 2, the target,
///     the count, then for each entry its cliloc in four bytes, its index and its flags.
/// </remarks>
[PacketHandler(0xBF, PacketSizing.Variable, MinimumLength = HeaderLength, Description = "Display context menu")]
public sealed class DisplayContextMenuPacket : BasePacket<DisplayContextMenuPacket>, IOutgoingPacket
{
    private const ushort Subcommand = 0x14;
    private const ushort Format = 0x02;
    private const int HeaderLength = 12;
    private const int EntryLength = 8;
    private const ushort GreyedFlag = 0x01;

    private readonly IReadOnlyList<(int Cliloc, bool Greyed)> _entries;

    public override int Length => HeaderLength + _entries.Count * EntryLength;

    public Serial Target { get; }

    /// <summary>
    ///     The entries in the order of their indexes: the text of the client and whether it is greyed out.
    /// </summary>
    public IReadOnlyList<(int Cliloc, bool Greyed)> Entries => _entries;

    public DisplayContextMenuPacket(Serial target, IReadOnlyList<(int Cliloc, bool Greyed)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count > byte.MaxValue)
        {
            throw new ArgumentException("A context menu holds 255 entries at most.", nameof(entries));
        }

        Target = target;
        _entries = entries;
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteUInt16BigEndian(Subcommand);
        writer.WriteUInt16BigEndian(Format);
        writer.WriteSerial(Target);
        writer.WriteByte((byte)_entries.Count);

        for (var index = 0; index < _entries.Count; index++)
        {
            writer.WriteUInt32BigEndian((uint)_entries[index].Cliloc);
            writer.WriteUInt16BigEndian((ushort)index);
            writer.WriteUInt16BigEndian(_entries[index].Greyed ? GreyedFlag : (ushort)0);
        }
    }
}
