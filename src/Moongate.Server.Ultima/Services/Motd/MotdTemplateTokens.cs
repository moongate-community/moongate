using System.Text.RegularExpressions;
using Moongate.Server.Ultima.Services.Text;

namespace Moongate.Server.Ultima.Services.Motd;

/// <summary>
///     Recognizes complete MOTD variable tokens in the original template text.
/// </summary>
public static class MotdTemplateTokens
{
    public static MatchCollection Find(string template)
    {
        return TextTemplateTokens.FindMotd(template);
    }

    public static bool IsValidName(string name)
    {
        return TextTemplateTokens.IsValidName(name);
    }
}
