using System.Text;

namespace Moongate.Server.Ultima.Packets.Books.Internal;

/// <summary>
///     Reads the texts of the book packets a client sends. Nothing is trusted: a text that is not what the packet
///     says it is gives no text.
/// </summary>
internal static class BookPacketText
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    /// <summary>
    ///     Reads a UTF-8 text ended by a zero, moving <paramref name="position" /> past the zero; false when the data
    ///     ends before the zero or the bytes are not UTF-8.
    /// </summary>
    public static bool TryReadZeroEnded(ReadOnlySpan<byte> data, ref int position, out string text)
    {
        text = "";
        var end = position > data.Length ? -1 : data[position..].IndexOf((byte)0);

        if (end < 0)
        {
            return false;
        }

        if (!TryDecode(data.Slice(position, end), out text))
        {
            return false;
        }

        position += end + 1;

        return true;
    }

    /// <summary>
    ///     Reads a UTF-8 text held in <paramref name="length" /> bytes, up to its first zero, moving
    ///     <paramref name="position" /> past the field.
    /// </summary>
    public static bool TryReadField(ReadOnlySpan<byte> data, ref int position, int length, out string text)
    {
        text = "";

        if (length < 0 || position + length > data.Length)
        {
            return false;
        }

        var field = data.Slice(position, length);
        var zero = field.IndexOf((byte)0);

        if (!TryDecode(zero < 0 ? field : field[..zero], out text))
        {
            return false;
        }

        position += length;

        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> bytes, out string text)
    {
        try
        {
            text = Utf8.GetString(bytes);

            return true;
        }
        catch (DecoderFallbackException)
        {
            text = "";

            return false;
        }
    }
}
