using Moongate.Server.Types.Commands;
using Moongate.UO.Data.Ids;
using Serilog.Events;

namespace Moongate.Server.Data.Internal.Commands;

/// <summary>
/// Carries parsed command metadata and output callback for command handlers.
/// </summary>
public sealed class CommandSystemContext
{
    private readonly Action<string, LogEventLevel> _printAction;

    public CommandSourceType Source { get; }

    public string CommandText { get; }

    public long SessionId { get; }

    public Serial CharacterId { get; }

    public bool IsInGame => Source == CommandSourceType.InGame;

    public long? SessionIdOrNull => IsInGame ? SessionId : null;

    public uint? CharacterIdOrNull => IsInGame && CharacterId.IsValid ? (uint)CharacterId : null;

    public string[] Arguments { get; }

    public CommandSystemContext(
        string commandText,
        string[] arguments,
        CommandSourceType source,
        long sessionId,
        Action<string, LogEventLevel> printAction,
        Serial characterId = default
    )
    {
        CommandText = commandText;
        Arguments = arguments;
        Source = source;
        _printAction = printAction;
        SessionId = sessionId;
        CharacterId = characterId;
    }

    public void Print(string message, params object[] args)
    {
        var formatted = args.Length == 0 ? message : string.Format(message, args);
        _printAction(formatted, LogEventLevel.Information);
    }

    public void PrintError(string message, params object[] args)
    {
        var formatted = args.Length == 0 ? message : string.Format(message, args);
        _printAction(formatted, LogEventLevel.Error);
    }

    public void PrintWarning(string message, params object[] args)
    {
        var formatted = args.Length == 0 ? message : string.Format(message, args);
        _printAction(formatted, LogEventLevel.Warning);
    }

    protected long GetSessionId()
    {
        if (Source != CommandSourceType.InGame)
        {
            throw new InvalidOperationException("Session ID is only available for in-game commands.");
        }

        return SessionId;
    }
}
