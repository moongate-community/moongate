using System.Diagnostics.CodeAnalysis;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Packets.Characters;

/// <summary>
///     The character the client chose to play from the character list (0x5D). 73 bytes, big-endian.
/// </summary>
/// <remarks>
///     The server identifies the character by <see cref="CharacterIndex" />, its position in the list it sent; the name
///     is what the client showed, and is not trusted. The packet also carries the client's IP address, which is not
///     used.
/// </remarks>
[PacketHandler(0x5D, PacketSizing.Fixed, Length = 73, Description = "Play character")]
public sealed class PlayCharacterPacket : BaseFixedPacket<PlayCharacterPacket>, IIncomingPacket<PlayCharacterPacket>
{
    private const int NameLength = 30;

    /// <summary>
    ///     Gets the name the client showed for the character.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets what the client says it can show, mostly the maps it has installed.
    /// </summary>
    public required ClientFlags ClientFlags { get; init; }

    /// <summary>
    ///     Gets how many times the client has logged in, as it counts them.
    /// </summary>
    public required uint LoginCount { get; init; }

    /// <summary>
    ///     Gets the character's position in the character list, counted from 0.
    /// </summary>
    public required int CharacterIndex { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out PlayCharacterPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        // Opcode, then the fixed 0xEDEDEDED pattern.
        var reader = new PacketReader(data[5..]);

        if (!reader.TryReadFixedAscii(NameLength, out var name) ||
            !reader.TryReadBytes(2, out _) ||
            !reader.TryReadUInt32BigEndian(out var clientFlags) ||
            !reader.TryReadBytes(4, out _) ||
            !reader.TryReadUInt32BigEndian(out var loginCount) ||
            !reader.TryReadBytes(16, out _) ||
            !reader.TryReadUInt32BigEndian(out var index))
        {
            return false;
        }

        // Safe: the Try read above succeeded, so the string is set.
        packet = new()
        {
            Name = name!,
            ClientFlags = (ClientFlags)clientFlags,
            LoginCount = loginCount,
            CharacterIndex = unchecked((int)index)
        };

        return true;
    }
}
