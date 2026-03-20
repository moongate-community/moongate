using Moongate.Server.Types.Commands;

namespace Moongate.Server.Data.Internal.Commands;

/// <summary>
/// Lua-safe wrapper over command execution context.
/// </summary>
public sealed class LuaCommandContext
{
    private readonly CommandSystemContext _context;

    public string CommandText => _context.CommandText;

    public string[] Arguments => _context.Arguments;

    public CommandSourceType Source => _context.Source;

    public bool IsInGame => _context.IsInGame;

    public long? SessionId => _context.SessionIdOrNull;

    public uint? CharacterId => _context.CharacterIdOrNull;

    public LuaCommandContext(CommandSystemContext context)
    {
        _context = context;
    }

    public void Print(string message, params object[] args)
        => _context.Print(message, args);

    public void PrintError(string message, params object[] args)
        => _context.PrintError(message, args);

    public void PrintWarning(string message, params object[] args)
        => _context.PrintWarning(message, args);
}
