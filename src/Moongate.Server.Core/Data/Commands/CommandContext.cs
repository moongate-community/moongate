using System.Globalization;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Commands;

namespace Moongate.Server.Core.Data.Commands;

/// <summary>Carries one parsed command invocation and accumulates the lines its handler produces.</summary>
public sealed class CommandContext
{
    private readonly List<CommandOutputLine> _output = [];

    /// <summary>Gets the raw command line as submitted, including the command name.</summary>
    public string CommandLine { get; }

    /// <summary>Gets the normalized alias the caller typed.</summary>
    public string CommandName { get; }

    /// <summary>Gets the whitespace-separated tokens following the command name.</summary>
    public string[] Arguments { get; }

    /// <summary>Gets the source that submitted the command.</summary>
    public CommandSourceType Source { get; }

    /// <summary>Gets the invoking session, or null when the command did not come from a session.</summary>
    public GameSession? Session { get; }

    /// <summary>Gets the token cancelling this invocation.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Gets whether the command was submitted from inside the game.</summary>
    public bool IsInGame => Source == CommandSourceType.InGame;

    /// <summary>Gets the lines produced so far, in the order they were written.</summary>
    public IReadOnlyList<CommandOutputLine> Output => _output;

    public CommandContext(
        string commandLine,
        string commandName,
        string[] arguments,
        CommandSourceType source,
        GameSession? session,
        CancellationToken cancellationToken = default
    )
    {
        CommandLine = commandLine;
        CommandName = commandName;
        Arguments = arguments;
        Source = source;
        Session = session;
        CancellationToken = cancellationToken;
    }

    /// <summary>Writes an informational line.</summary>
    public void Print(string message, params object[] args)
    {
        Write(message, args, CommandOutputLevel.Information);
    }

    /// <summary>Writes a warning line.</summary>
    public void PrintWarning(string message, params object[] args)
    {
        Write(message, args, CommandOutputLevel.Warning);
    }

    /// <summary>Writes an error line.</summary>
    public void PrintError(string message, params object[] args)
    {
        Write(message, args, CommandOutputLevel.Error);
    }

    private void Write(string message, object[] args, CommandOutputLevel level)
    {
        var formatted = args.Length == 0 ? message : string.Format(CultureInfo.InvariantCulture, message, args);

        // A single-line message is kept verbatim so an empty echo still produces one line;
        // only genuinely multi-line text is split, and its blank segments are dropped.
        if (!formatted.Contains('\n'))
        {
            _output.Add(new CommandOutputLine(formatted.TrimEnd('\r'), level));

            return;
        }

        foreach (var line in formatted.Split('\n'))
        {
            var normalized = line.TrimEnd('\r');

            if (normalized.Length > 0)
            {
                _output.Add(new CommandOutputLine(normalized, level));
            }
        }
    }
}
