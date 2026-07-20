using Moongate.Console.Admin.Plugin.Types;

namespace Moongate.Console.Admin.Plugin.Services.Console;

/// <summary>Classifies a console input line. Pure — no I/O.</summary>
public static class ConsoleInput
{
    public static ConsoleInputKind Classify(string line)
    {
        var trimmed = line.Trim();

        if (trimmed.Length == 0)
        {
            return ConsoleInputKind.Empty;
        }

        if (trimmed.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleInputKind.Help;
        }

        if (trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleInputKind.Quit;
        }

        return ConsoleInputKind.Command;
    }
}
