using System.Globalization;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.BulletinBoards;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>board</c> Lua module: the bulletin boards, for the item script that opens one on a double click and for
///     the scripts that write on one; <c>board.open(serial, user)</c>, <c>board.post(serial, "The town crier",
///     "Hear ye", { "The bank is closed." })</c>.
/// </summary>
[ScriptModule("board", "The bulletin boards: items whose template has the script bulletin_board, each with its own messages. A script opens one on a player, posts on it in any name, lists its messages and removes one. The module does not check who calls it nor how far anyone stands: the item script's on_use already asks for two tiles and sight, and a script for the staff checks world.is_staff first.")]
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

    /// <summary>
    ///     Posts on a board in the name given; <c>board.post(serial, "The town crier", "Hear ye", { "The bank is
    ///     closed." })</c>.
    /// </summary>
    [ScriptFunction(helpText: "Posts a message on the bulletin board in the name given and gives its serial; nil when nothing was posted: the item is not a bulletin board, the name, the subject or every line is empty, no serial was ready (the same call works a moment later), or thread is not a serial at all. lines is an array of strings, one per line of the message; a number among them is written as text, anything else is left out, and the lines end at the first nil, as ipairs does. With thread, the serial of a message of that board, it is a reply and goes under the first message of that thread; a thread that is gone or of another board makes it a new thread. No character is the poster: the post does not wait, shows a bare body beside the text, and only the staff can remove it from the board's window. The name is cut at 30 characters, the subject at 60, a line at 80 and the message at 32 lines; a board beyond ultima.bulletin_boards.max_messages lets its oldest thread go. A player with the board open sees the post when it opens the board again.")]
    public long? Post(long board, string name, string subject, LuaTable lines, long thread = 0)
    {
        if (thread is < 0 or > uint.MaxValue || !TryGetBoard(board, out var item))
        {
            return null;
        }

        var result = _boards.PostAs(item, name, subject, Lines(lines), new Serial((uint)thread));

        return result.Type == BulletinPostResultType.Ok ? result.Message!.Id.Value : null;
    }

    /// <summary>
    ///     Gets the messages of a board; <c>for _, message in ipairs(board.messages(serial)) do ... end</c>. Each is
    ///     <c>{ serial, thread, poster, name, subject, lines, posted_at }</c>.
    /// </summary>
    [ScriptFunction(helpText: "The messages of the bulletin board as an array of { serial, thread, poster, name, subject, lines, posted_at }: the threads from the oldest, each followed by its replies in the order they were posted. thread is the serial of the first message of the thread a reply is under, nil on a first message. poster is the serial of the character who posted, nil for a message a script posted; name is the name it posted under. lines is an array of strings. posted_at is in seconds, as world.now(). An empty array for a board with no message and for what is not a board.")]
    public LuaTable Messages(long board)
    {
        var list = new LuaTable();

        if (!IsSerial(board))
        {
            return list;
        }

        var index = 1;

        foreach (var message in _boards.GetMessages(new Serial((uint)board)))
        {
            list[index++] = ToTable(message);
        }

        return list;
    }

    /// <summary>
    ///     Removes a message from its board; <c>board.remove(message)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Removes the message with that serial from its bulletin board, and its replies with it when it is the first message of a thread; false when there is no such message. It asks nobody: a script that removes for a player checks first that the message is the player's, with the poster of board.messages.")]
    public bool Remove(long message)
    {
        return IsSerial(message) && _boards.Remove(new Serial((uint)message)).Count > 0;
    }

    private static LuaTable ToTable(BulletinMessageEntity message)
    {
        var table = new LuaTable();
        table["serial"] = (long)message.Id.Value;

        if (!message.IsThread)
        {
            table["thread"] = (long)message.ThreadId.Value;
        }

        if (message.PosterId.IsValid)
        {
            table["poster"] = (long)message.PosterId.Value;
        }

        table["name"] = message.PosterName;
        table["subject"] = message.Subject;

        var lines = new LuaTable();
        var index = 1;

        foreach (var line in message.Lines())
        {
            lines[index++] = line;
        }

        table["lines"] = lines;
        table["posted_at"] = message.PostedAt / 1000;

        return table;
    }

    // The lines of a post from a Lua array: its strings, a number as its text, nothing else.
    private static List<string> Lines(LuaTable lines)
    {
        var text = new List<string>();

        for (var index = 1; index <= lines.ArrayLength; index++)
        {
            var value = lines[index];

            if (value.TryRead<string>(out var line))
            {
                text.Add(line);
            }
            else if (value.TryRead<double>(out var number))
            {
                text.Add(number.ToString(CultureInfo.InvariantCulture));
            }
        }

        return text;
    }

    private bool TryGetBoard(long board, out ItemEntity item)
    {
        item = null!;

        return IsSerial(board) && _items.TryGet(new Serial((uint)board), out item!) && _boards.IsBoard(item);
    }

    private static bool IsSerial(long value)
    {
        return value is > 0 and <= uint.MaxValue;
    }
}
