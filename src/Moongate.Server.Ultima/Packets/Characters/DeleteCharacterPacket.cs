using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Server.Ultima.Packets.Characters;

/// <summary>
///     The character the client asks to delete (0x83), by its position in the character list it was sent. 39 bytes,
///     big-endian.
/// </summary>
/// <remarks>
///     The packet also carries the account password and the client's IP address; neither is used.
/// </remarks>
[PacketHandler(0x83, PacketSizing.Fixed, Length = 39, Description = "Delete character")]
public sealed class DeleteCharacterPacket : BaseFixedPacket<DeleteCharacterPacket>, IIncomingPacket<DeleteCharacterPacket>
{
    private const int PasswordLength = 30;

    /// <summary>
    ///     Gets the character's position in the character list, counted from 0.
    /// </summary>
    public required int CharacterIndex { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out DeleteCharacterPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadBytes(PasswordLength, out _) || !reader.TryReadUInt32BigEndian(out var index))
        {
            return false;
        }

        packet = new() { CharacterIndex = unchecked((int)index) };

        return true;
    }
}
