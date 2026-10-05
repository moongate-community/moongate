using System.Text;

namespace Moongate.Server.Ultima.Packets.BulletinBoards.Internal;

/// <summary>
///     The strings of packet 0x71: UTF-8, behind a length byte that counts the zeros after the text.
/// </summary>
internal static class BulletinBoardText
{
    /// <summary>
    ///     The most bytes of text a string holds: its length byte also counts its one zero.
    /// </summary>
    public const int MaxString = 254;

    /// <summary>
    ///     The most bytes of text a line holds: its length byte also counts its two zeros.
    /// </summary>
    public const int MaxLine = 253;

    /// <summary>
    ///     Gets the UTF-8 bytes of <paramref name="text" />, no more than <paramref name="maxBytes" /> and never the
    ///     first half of a character.
    /// </summary>
    public static byte[] Cut(string? text, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");

        if (bytes.Length <= maxBytes)
        {
            return bytes;
        }

        var length = maxBytes;

        // A continuation byte is 10xxxxxx: the cut moves back to where a character starts.
        while (length > 0 && (bytes[length] & 0xC0) == 0x80)
        {
            length--;
        }

        return bytes[..length];
    }
}
