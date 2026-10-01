using System.Text;

namespace Moongate.Server.Ultima.Data.Gumps;

/// <summary>
///     One command of a gump layout, written as the client reads it: <c>{ resizepic 0 0 9200 300 400 }</c>.
/// </summary>
public abstract class GumpEntry
{
    /// <summary>
    ///     Appends the command to <paramref name="layout" />, putting its texts in <paramref name="strings" />.
    /// </summary>
    public abstract void Write(StringBuilder layout, GumpStrings strings);

    protected static int Flag(bool value)
    {
        return value ? 1 : 0;
    }

    /// <summary>
    ///     Removes from cliloc arguments the characters that would end them or open a new command: <c>@</c>, <c>{</c>
    ///     and <c>}</c>.
    /// </summary>
    protected static string Arguments(string args)
    {
        return string.Concat(args.Where(character => character is not ('@' or '{' or '}')));
    }
}
