namespace Moongate.Ctl.Internal;

/// <summary>
///     Reads mgctl's command line before the commands run: the older spelling without <c>init</c> is turned into
///     <c>init</c>, and a line that names no command is refused instead of answered with the help and a zero exit code.
/// </summary>
internal static class CommandLine
{
    private static readonly Dictionary<string, string[]> Commands = new(StringComparer.Ordinal)
    {
        ["init"] = [],
        ["migrate"] = ["status", "apply"],
        ["completion"] = []
    };

    // What a command of one word needs after it.
    private static readonly Dictionary<string, string> Arguments = new(StringComparer.Ordinal)
    {
        ["init"] = "a root directory",
        ["completion"] = "a shell: bash, zsh or fish"
    };

    /// <summary>
    ///     Gets the arguments to run, or null with <paramref name="error" /> set for a line that names no command.
    /// </summary>
    public static string[]? Read(string[] args, out string? error)
    {
        error = null;

        if (args.Length == 0 || IsGlobalOption(args[0]))
        {
            return args;
        }

        if (Commands.TryGetValue(args[0], out var subcommands))
        {
            if (subcommands.Length == 0)
            {
                if (args.Length == 1)
                {
                    error = $"'{args[0]}' needs {Arguments[args[0]]}.";

                    return null;
                }

                return args;
            }

            if (args.Length == 1 || !subcommands.Contains(args[1]) && !IsHelp(args[1]))
            {
                error = $"'{args[0]}' needs one of: {string.Join(", ", subcommands)}.";

                return null;
            }

            return args;
        }

        // The spelling of mgboot: a root, or its options, without "init".
        if (args[0].StartsWith('-') || LooksLikeAPath(args[0]))
        {
            return ["init", .. args];
        }

        error = $"unknown command '{args[0]}'; to prepare a root of that name, use: mgctl init {args[0]}";

        return null;
    }

    private static bool IsGlobalOption(string arg)
    {
        return IsHelp(arg) || arg == "--version";
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help";
    }

    // A lone word such as "help" or a mistyped command must not become a new root.
    private static bool LooksLikeAPath(string arg)
    {
        return arg.Contains('/') ||
               arg.Contains('\\') ||
               arg.StartsWith('.') ||
               arg.StartsWith('~') ||
               Path.IsPathRooted(arg) ||
               Directory.Exists(arg);
    }
}
