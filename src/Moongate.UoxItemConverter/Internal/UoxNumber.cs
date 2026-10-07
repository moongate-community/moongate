using System.Globalization;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads a UOX3 number as UOX3 does ( <c>stoi(value, nullptr, 0)</c>): hex with <c>0x</c> or decimal, from the
///     first
///     token. It also forgives the typos real UOX3 data has: a doubled prefix ( <c>0x0x04FC</c>) and trailing
///     punctuation ( <c>0x15b6]</c>).
/// </summary>
internal static class UoxNumber
{
    public static bool TryParse(string text, out int value)
    {
        text = text.Trim();
        var space = text.IndexOfAny([' ', '\t']);

        if (space >= 0)
        {
            text = text[..space];
        }

        text = text.TrimEnd(']', ',', ';');

        if (text.StartsWith("0x0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], NumberStyles.HexNumber, null, out value)
            : int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }
}
