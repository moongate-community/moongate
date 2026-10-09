using System.Text;
using Moongate.Ctl.Data.Internal.Completion;
using Moongate.Ctl.Types.Completion;

namespace Moongate.Ctl.Internal;

/// <summary>
///     Writes the completion scripts of bash, zsh and fish from the commands of <see cref="CompletionCatalog" />: the
///     first word, the second word of <c>migrate</c>, then the options of the command and what
///     follows each (a directory or a fixed word).
/// </summary>
internal static class CompletionScripts
{
    private const string GlobalOptions = "--help --version";

    public static string Bash(IReadOnlyList<CompletionCommand> commands)
    {
        var text = new StringBuilder();
        text.AppendLine("# bash completion for mgctl; load it with: source <(mgctl completion bash)");
        text.AppendLine("_mgctl_paths() {");
        text.AppendLine("    local IFS=$'\\n'");
        text.AppendLine("    compopt -o filenames 2>/dev/null");
        text.AppendLine("    COMPREPLY=( $(compgen \"$1\" -- \"$2\") )");
        text.AppendLine("}");
        text.AppendLine();
        text.AppendLine("_mgctl() {");
        text.AppendLine("    local cur prev command");
        text.AppendLine("    cur=\"${COMP_WORDS[COMP_CWORD]}\"");
        text.AppendLine("    prev=\"${COMP_WORDS[COMP_CWORD-1]}\"");
        text.AppendLine("    COMPREPLY=()");
        text.AppendLine();
        text.AppendLine("    if [ \"$COMP_CWORD\" -eq 1 ]; then");
        text.AppendLine(
            $"        COMPREPLY=( $(compgen -W \"{string.Join(' ', FirstWords(commands))} {GlobalOptions}\" -- \"$cur\") )"
        );
        text.AppendLine("        return");
        text.AppendLine("    fi");
        text.AppendLine();
        text.AppendLine("    command=\"${COMP_WORDS[1]}\"");
        text.AppendLine();
        text.AppendLine("    case \"$command\" in");

        foreach (var group in Groups(commands))
        {
            text.AppendLine($"        {group.Key})");
            text.AppendLine("            if [ \"$COMP_CWORD\" -eq 2 ]; then");
            text.AppendLine(
                $"                COMPREPLY=( $(compgen -W \"{string.Join(' ', group.Select(SecondWord))}\" -- \"$cur\") )"
            );
            text.AppendLine("                return");
            text.AppendLine("            fi");
            text.AppendLine();
            text.AppendLine("            command=\"$command ${COMP_WORDS[2]}\"");
            text.AppendLine("            ;;");
        }

        text.AppendLine("    esac");
        text.AppendLine();
        text.AppendLine("    case \"$command $prev\" in");

        foreach (var command in commands)
        {
            foreach (var option in command.Options.Where(option => option.Value != CompletionValueType.None))
            {
                text.AppendLine($"        \"{command.Path} {option.Name}\")");

                if (BashValue(option) is { } reply)
                {
                    text.AppendLine($"            {reply}");
                }

                text.AppendLine("            return");
                text.AppendLine("            ;;");
            }
        }

        text.AppendLine("    esac");
        text.AppendLine();
        text.AppendLine("    case \"$command\" in");

        foreach (var command in commands)
        {
            var options = string.Join(' ', command.Options.Select(option => option.Name).Append("--help"));
            text.AppendLine($"        \"{command.Path}\")");

            if (command.Argument is { } argument && BashValue(argument) is { } reply)
            {
                text.AppendLine("            if [[ \"$cur\" == -* ]]; then");
                text.AppendLine($"                COMPREPLY=( $(compgen -W \"{options}\" -- \"$cur\") )");
                text.AppendLine("            else");
                text.AppendLine($"                {reply}");
                text.AppendLine("            fi");
            }
            else
            {
                text.AppendLine($"            COMPREPLY=( $(compgen -W \"{options}\" -- \"$cur\") )");
            }

            text.AppendLine("            ;;");
        }

        text.AppendLine("    esac");
        text.AppendLine("}");
        text.AppendLine();
        text.AppendLine("complete -F _mgctl mgctl");

        return text.ToString();
    }

    public static string Zsh(IReadOnlyList<CompletionCommand> commands)
    {
        var text = new StringBuilder();
        text.AppendLine("#compdef mgctl");
        text.AppendLine("# zsh completion for mgctl; load it with: source <(mgctl completion zsh)");
        text.AppendLine();
        text.AppendLine("_mgctl() {");
        text.AppendLine("    local -a commands");
        text.AppendLine();
        text.AppendLine("    if (( CURRENT == 2 )); then");
        text.AppendLine(
            $"        commands=({string.Join(' ', FirstWords(commands).Select(word => ZshDescribed(word, FirstWordHelp(commands, word))))})"
        );
        text.AppendLine("        _describe 'command' commands");
        text.AppendLine("        return");
        text.AppendLine("    fi");
        text.AppendLine();
        text.AppendLine("    case \"$words[2]\" in");

        foreach (var group in Groups(commands))
        {
            text.AppendLine($"        {group.Key})");
            text.AppendLine("            if (( CURRENT == 3 )); then");
            text.AppendLine(
                $"                commands=({string.Join(' ', group.Select(command => ZshDescribed(SecondWord(command), command.Help)))})"
            );
            text.AppendLine("                _describe 'command' commands");
            text.AppendLine("                return");
            text.AppendLine("            fi");
            text.AppendLine();
            text.AppendLine("            case \"$words[3]\" in");

            foreach (var command in group)
            {
                text.AppendLine($"                {SecondWord(command)})");
                text.AppendLine("                    shift 2 words");
                text.AppendLine("                    (( CURRENT -= 2 ))");
                text.AppendLine($"                    _arguments {ZshArguments(command)}");
                text.AppendLine("                    ;;");
            }

            text.AppendLine("            esac");
            text.AppendLine("            ;;");
        }

        foreach (var command in commands.Where(command => !command.Path.Contains(' ')))
        {
            text.AppendLine($"        {command.Path})");
            text.AppendLine("            shift words");
            text.AppendLine("            (( CURRENT-- ))");
            text.AppendLine($"            _arguments {ZshArguments(command)}");
            text.AppendLine("            ;;");
        }

        text.AppendLine("    esac");
        text.AppendLine("}");
        text.AppendLine();
        text.AppendLine("# Sourced, it registers itself; autoloaded from fpath, it runs as the completion function.");
        text.AppendLine("if [ \"$funcstack[1]\" = \"_mgctl\" ]; then");
        text.AppendLine("    _mgctl \"$@\"");
        text.AppendLine("else");
        text.AppendLine("    compdef _mgctl mgctl");
        text.AppendLine("fi");

        return text.ToString();
    }

    public static string Fish(IReadOnlyList<CompletionCommand> commands)
    {
        var text = new StringBuilder();
        text.AppendLine("# fish completion for mgctl; load it with: mgctl completion fish | source");
        text.AppendLine();
        text.AppendLine("# Whether the words after mgctl start with the given ones: a root or a value named like a command");
        text.AppendLine("# further on the line does not count.");
        text.AppendLine("function __mgctl_is");
        text.AppendLine("    set -l tokens (commandline -opc)");
        text.AppendLine("    test (count $tokens) -gt (count $argv); or return 1");
        text.AppendLine();
        text.AppendLine("    for index in (seq (count $argv))");
        text.AppendLine("        test \"$tokens[(math $index + 1)]\" = \"$argv[$index]\"; or return 1");
        text.AppendLine("    end");
        text.AppendLine("end");
        text.AppendLine();
        text.AppendLine("# Whether the line holds exactly this many words after mgctl before the one being typed.");
        text.AppendLine("function __mgctl_words");
        text.AppendLine("    test (count (commandline -opc)) -eq (math $argv[1] + 1)");
        text.AppendLine("end");
        text.AppendLine();
        text.AppendLine("complete -c mgctl -f");

        foreach (var word in FirstWords(commands))
        {
            text.AppendLine(
                $"complete -c mgctl -n '__mgctl_words 0' -a {word} -d '{Quoted(FirstWordHelp(commands, word))}'"
            );
        }

        foreach (var group in Groups(commands))
        {
            foreach (var command in group)
            {
                text.AppendLine(
                    $"complete -c mgctl -n '__mgctl_is {group.Key}; and __mgctl_words 1' -a {SecondWord(command)} -d '{Quoted(command.Help)}'"
                );
            }
        }

        foreach (var command in commands)
        {
            var condition = $"__mgctl_is {command.Path}";

            foreach (var option in command.Options)
            {
                text.AppendLine(
                    $"complete -c mgctl -n '{condition}' -l {option.Name[2..]}{FishValue(option)} -d '{Quoted(option.Help)}'"
                );
            }

            if (command.Argument is { } argument)
            {
                var words = argument.Value == CompletionValueType.Choice
                    ? $"'{string.Join(' ', argument.Choices)}'"
                    : "'(__fish_complete_directories)'";
                text.AppendLine($"complete -c mgctl -n '{condition}' -a {words} -d '{Quoted(argument.Help)}'");
            }
        }

        return text.ToString();
    }

    // The first words in the order of the catalog: init, migrate, completion.
    private static IEnumerable<string> FirstWords(IReadOnlyList<CompletionCommand> commands)
    {
        return commands.Select(command => command.Path.Split(' ')[0]).Distinct();
    }

    // The commands of two words, by their first: migrate.
    private static IEnumerable<IGrouping<string, CompletionCommand>> Groups(IReadOnlyList<CompletionCommand> commands)
    {
        return commands.Where(command => command.Path.Contains(' ')).GroupBy(command => command.Path.Split(' ')[0]);
    }

    private static string SecondWord(CompletionCommand command)
    {
        return command.Path.Split(' ')[1];
    }

    private static string FirstWordHelp(IReadOnlyList<CompletionCommand> commands, string word)
    {
        return word switch
        {
            "migrate" => "List or apply the database migrations",
            _         => commands.First(command => command.Path == word).Help
        };
    }

    // What bash offers for the value; null for free text.
    private static string? BashValue(CompletionOption option)
    {
        return option.Value switch
        {
            CompletionValueType.Choice => $"COMPREPLY=( $(compgen -W \"{string.Join(' ', option.Choices)}\" -- \"$cur\") )",
            CompletionValueType.Directory => "_mgctl_paths -d \"$cur\"",
            _ => null
        };
    }

    private static string ZshArguments(CompletionCommand command)
    {
        var specs = command.Options.Select(option => $"'{option.Name}[{ZshText(option.Help)}]{ZshValue(option)}'").ToList();

        if (command.Argument is { } argument)
        {
            specs.Add($"'1:{ZshText(argument.Help)}{ZshValue(argument)[ZshValue(argument).IndexOf(':', 1)..]}'");
        }

        return string.Join(" \\\n                        ", specs);
    }

    // The ":message:action" a zsh spec ends with; nothing for a flag.
    private static string ZshValue(CompletionOption option)
    {
        return option.Value switch
        {
            CompletionValueType.Choice    => $":value:({string.Join(' ', option.Choices)})",
            CompletionValueType.Directory => ":directory:_files -/",
            CompletionValueType.Text      => ":value:",
            _                             => ""
        };
    }

    private static string ZshDescribed(string word, string help)
    {
        return $"'{word}:{ZshText(help)}'";
    }

    // Colons and brackets end a zsh spec, a quote ends its string.
    private static string ZshText(string help)
    {
        return Quoted(help)
            .Replace(":", "\\:", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

    private static string FishValue(CompletionOption option)
    {
        return option.Value switch
        {
            CompletionValueType.Choice    => $" -r -a '{string.Join(' ', option.Choices)}'",
            CompletionValueType.Directory => " -r -a '(__fish_complete_directories)'",
            CompletionValueType.Text      => " -r",
            _                             => ""
        };
    }

    private static string Quoted(string help)
    {
        return help.Replace("'", "'\\''", StringComparison.Ordinal);
    }
}
