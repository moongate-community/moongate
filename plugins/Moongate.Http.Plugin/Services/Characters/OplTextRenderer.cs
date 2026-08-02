using System.Globalization;
using System.Text.RegularExpressions;
using Moongate.Network.Data;
using Moongate.Server.Abstractions.Interfaces.Localization;

namespace Moongate.Http.Plugin.Services.Characters;

/// <summary>
/// Turns one object-property entry into a line of text: the cliloc's own string with its arguments
/// substituted. This lives on the server because the grammar is UO's — TAB-separated slots,
/// <c>~N_X~</c> placeholders, and an argument that may itself be a <c>#cliloc</c> reference. A browser
/// should receive finished sentences, not a numbering scheme to decode.
/// </summary>
public sealed partial class OplTextRenderer
{
    private readonly IClilocService _clilocs;

    public OplTextRenderer(IClilocService clilocs)
    {
        _clilocs = clilocs;
    }

    /// <summary>
    /// The entry as a line, or null when the string table cannot describe its cliloc — which is the
    /// normal case on a shard whose client files are absent, and a reason to show no line rather than
    /// to fail.
    /// </summary>
    public string? Render(OplEntry entry)
    {
        var text = _clilocs.Text(entry.Cliloc);

        if (text is null)
        {
            return null;
        }

        var arguments = entry.Arguments.Split('\t');

        // Slots are 1-based in the placeholder and positional in the argument list. A slot with no
        // argument — malformed data, or a cliloc declaring more than it was given — collapses to
        // nothing rather than leaving "~2_ITEMNAME~" in a sentence someone reads.
        return Placeholder()
               .Replace(
                   text,
                   match =>
                   {
                       var slot = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                       return slot <= arguments.Length ? Resolve(arguments[slot - 1]) : string.Empty;
                   }
               )
               .Trim();
    }

    [GeneratedRegex(@"~(\d+)_[^~]*~")]
    private static partial Regex Placeholder();

    /// <summary>An argument prefixed with # is a cliloc of its own; anything else is already text.</summary>
    private string Resolve(string argument)
    {
        if (!argument.StartsWith('#'))
        {
            return argument;
        }

        return int.TryParse(argument[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cliloc)
                   ? _clilocs.Text(cliloc) ?? string.Empty
                   : string.Empty;
    }
}
