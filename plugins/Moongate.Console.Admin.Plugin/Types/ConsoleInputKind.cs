namespace Moongate.Console.Admin.Plugin.Types;

/// <summary>What a console input line is: a built-in or a command to dispatch.</summary>
public enum ConsoleInputKind
{
    Empty,
    Help,
    Quit,
    Command
}
