using System.Text.RegularExpressions;

namespace Moongate.Server.Ultima.Utils;

/// <summary>
///     The rule a template's <c>script_id</c> follows: it names a global Lua table and the file that defines it, so it
///     is
///     a lower-case Lua identifier.
/// </summary>
public static partial class ScriptIdUtils
{
    public const string Rule = "must be a Lua identifier of lower-case letters, digits and underscores";

    /// <summary>
    ///     Gets whether <paramref name="scriptId" /> is a lower-case Lua identifier, such as <c>wander</c>.
    /// </summary>
    public static bool IsValid(string scriptId)
    {
        return ScriptIdPattern().IsMatch(scriptId);
    }

    [GeneratedRegex(@"^[a-z_][a-z0-9_]*\z")]
    private static partial Regex ScriptIdPattern();
}
