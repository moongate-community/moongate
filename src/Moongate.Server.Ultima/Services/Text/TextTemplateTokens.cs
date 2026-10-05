using System.Text.RegularExpressions;
using Moongate.Server.Ultima.Data.Internal.Text;
using Moongate.Server.Ultima.Types.Text;

namespace Moongate.Server.Ultima.Services.Text;

public static partial class TextTemplateTokens
{
    public static IReadOnlyList<TextTemplateToken> Find(string source, TextTemplateSyntaxType syntax)
    {
        var matches = syntax == TextTemplateSyntaxType.Motd ? FindMotd(source) : DocumentRegex().Matches(source);
        return matches.Select(match => new TextTemplateToken
        {
            Index = match.Index,
            Length = match.Length,
            Name = match.Value == "$$" ? null : match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value
        }).ToArray();
    }

    public static MatchCollection FindMotd(string source)
    {
        return MotdRegex().Matches(source);
    }

    public static bool IsValidName(string name)
    {
        return name.Length > 0 && name[0] is >= 'a' and <= 'z' &&
            name.Skip(1).All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }

    [GeneratedRegex(@"\$\{([^}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex MotdRegex();

    [GeneratedRegex(@"\$\$|\$\{([^}]*)\}|\$([a-z][a-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex DocumentRegex();
}
