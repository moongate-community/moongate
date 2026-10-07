using ConsoleAppFramework;

namespace Moongate.Ctl.Internal;

/// <summary>
///     The <c>mgctl completion</c> command: the script that lets a shell complete mgctl's commands and options with
///     TAB.
/// </summary>
internal static class CompletionCommands
{
    /// <summary>
    ///     Prints the script that completes mgctl with TAB in a shell.
    /// </summary>
    /// <param name="shell">
    ///     bash, zsh or fish.
    /// </param>
    public static int Completion([Argument] string shell)
    {
        var script = shell switch
        {
            "bash" => CompletionScripts.Bash(CompletionCatalog.Commands),
            "zsh"  => CompletionScripts.Zsh(CompletionCatalog.Commands),
            "fish" => CompletionScripts.Fish(CompletionCatalog.Commands),
            _      => null
        };

        if (script is null)
        {
            Console.Error.WriteLine(
                $"mgctl: no completion for '{shell}'; choose {string.Join(", ", CompletionCatalog.Shells)}."
            );

            return 2;
        }

        // The same line ends on every platform: a shell reads the script.
        Console.Out.Write(script.ReplaceLineEndings("\n"));

        return 0;
    }
}
