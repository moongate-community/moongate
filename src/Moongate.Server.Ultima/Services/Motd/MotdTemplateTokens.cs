using System.Text.RegularExpressions;

namespace Moongate.Server.Ultima.Services.Motd;

/// <summary>Recognizes complete MOTD variable tokens in the original template text.</summary>
public static partial class MotdTemplateTokens
{
    [GeneratedRegex(@"\$\{([^}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    public static MatchCollection Find(string template)
    {
        return TokenRegex().Matches(template);
    }

    public static bool IsValidName(string name)
    {
        return name.Length > 0 && name[0] is >= 'a' and <= 'z' &&
               name.Skip(1).All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');
    }
}
