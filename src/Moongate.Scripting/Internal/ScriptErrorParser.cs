using System.Text.RegularExpressions;
using Lua;
using Moongate.Scripting.Data.Scripts;

namespace Moongate.Scripting.Internal;

internal static partial class ScriptErrorParser
{
    public static ScriptErrorInfo Parse(string message, string? traceback)
    {
        ArgumentNullException.ThrowIfNull(message);
        var match = Position().Match(message);

        if (!match.Success)
        {
            return new ScriptErrorInfo("", 0, message.Trim(), traceback);
        }

        return new ScriptErrorInfo(
            match.Groups["file"].Value,
            int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture),
            match.Groups["message"].Value.Trim(),
            traceback
        );
    }

    public static ScriptErrorInfo FromException(Exception exception, string fallbackFile)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var traceback = exception is LuaRuntimeException runtime ? runtime.LuaTraceback.ToString() : null;
        var info = Parse(exception.Message, traceback);

        return info.File.Length == 0 ? info with { File = fallbackFile } : info;
    }

    [GeneratedRegex("""\[string "(?<file>[^"]+)"\]:(?<line>\d+):\s*(?<message>.*)""", RegexOptions.Singleline)]
    private static partial Regex Position();
}
