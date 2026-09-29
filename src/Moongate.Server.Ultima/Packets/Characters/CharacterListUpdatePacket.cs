using System.Collections.ObjectModel;
using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Characters;

/// <summary>
///     Sends the character slots again after a deletion (0x86): the same slots as the character list (0xA9), without
///     the cities and flags.
/// </summary>
[PacketHandler(0x86, PacketSizing.Variable, MinimumLength = 4)]
public sealed class CharacterListUpdatePacket : BasePacket<CharacterListUpdatePacket>, IOutgoingPacket
{
    private const int FixedLength = 4;
    private const int SlotNameLength = 30;
    private const int SlotEntryLength = 60;

    private static readonly int[] SupportedSlotCounts = [1, 5, 6, 7];

    public override int Length { get; }

    /// <summary>
    ///     Gets the name in each slot; null is an empty slot.
    /// </summary>
    public IReadOnlyList<string?> Characters { get; }

    public CharacterListUpdatePacket(IEnumerable<string?> characters)
    {
        ArgumentNullException.ThrowIfNull(characters);
        var slots = characters.ToArray();

        if (!SupportedSlotCounts.Contains(slots.Length))
        {
            throw new ArgumentException(
                $"The client supports 1, 5, 6 or 7 character slots, got {slots.Length}.",
                nameof(characters)
            );
        }

        if (slots.Any(name => name is not null && (name.Length > SlotNameLength || !Ascii.IsValid(name))))
        {
            throw new ArgumentException(
                $"Character names must be ASCII and at most {SlotNameLength} characters.",
                nameof(characters)
            );
        }

        Length = FixedLength + SlotEntryLength * slots.Length;
        Characters = new ReadOnlyCollection<string?>(slots);
    }

    public void Write(ref PacketWriter writer)
    {
        writer.EnsureCapacity(Length);
        writer.WriteByte(OpCode);
        writer.WriteUInt16BigEndian((ushort)Length);
        writer.WriteByte((byte)Characters.Count);

        foreach (var name in Characters)
        {
            writer.WriteFixedAscii(name ?? string.Empty, SlotNameLength);

            // Unused password field.
            writer.WriteFixedAscii(string.Empty, SlotEntryLength - SlotNameLength);
        }
    }
}
