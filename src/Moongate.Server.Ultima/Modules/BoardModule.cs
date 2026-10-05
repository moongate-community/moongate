using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>board</c> Lua module: the bulletin boards, for the item script that opens one on a double click;
///     <c>board.open(serial, user)</c>.
/// </summary>
[ScriptModule("board", "The bulletin boards: items whose template has the script bulletin_board, each with its own messages. The module does not check who calls it nor how far the player stands: the item script's on_use already asks for two tiles and sight.")]
public sealed class BoardModule
{
    private readonly IBulletinBoardService _boards;
    private readonly IItemService _items;
    private readonly ISessionService _sessions;

    public BoardModule(IBulletinBoardService boards, IItemService items, ISessionService sessions)
    {
        _boards = boards;
        _items = items;
        _sessions = sessions;
    }

    /// <summary>
    ///     Opens a bulletin board on a player; <c>board.open(serial, user)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Opens the bulletin board on the player's client: the threads that are over go first, then the client shows the board and lists its messages, and asks for each as the player reads. From then on the player reads, posts, replies and removes its own messages while it stays within two tiles of the board. False when the item is not a bulletin board, or the player is not in the world with a client.")]
    public bool Open(long board, long player)
    {
        return IsSerial(board) &&
               IsSerial(player) &&
               _items.TryGet(new Serial((uint)board), out var item) &&
               _boards.IsBoard(item) &&
               _sessions.TryGetByCharacterId(new Serial((uint)player), out var session) &&
               _boards.Open(item, session);
    }

    private static bool IsSerial(long value)
    {
        return value is > 0 and <= uint.MaxValue;
    }
}
