using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.BulletinBoards;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Types.BulletinBoards;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the messages of the bulletin boards, the rows of <c>world.bulletin_messages</c>: read at startup and
///     written by the world save. One repeating <c>bulletin_boards</c> timer sweeps the boards every hour.
/// </summary>
public sealed class BulletinBoardService : IBulletinBoardService
{
    public const string TimerName = "bulletin_boards";

    /// <summary>
    ///     The script of an item template that makes its items bulletin boards.
    /// </summary>
    public const string ScriptId = "bulletin_board";

    /// <summary>
    ///     The item the client draws for a message in the list of a board.
    /// </summary>
    public const int MessageItemId = 0x0EB0;

    /// <summary>
    ///     What a board without a name of its own is called on the client.
    /// </summary>
    public const string DefaultName = "bulletin board";

    public const int WaitMessage = 30155;
    public const int NotYoursMessage = 30156;
    public const int BusyMessage = 30157;

    /// <summary>
    ///     The body the client draws beside a message a script posted: a human, wearing nothing.
    /// </summary>
    public const int ScriptBody = 0x0190;

    /// <summary>
    ///     The longest name a script posts under.
    /// </summary>
    public const int MaxName = 30;

    public const int MaxSubject = 60;
    public const int MaxLine = 80;
    public const int MaxLines = 32;

    private const long MillisecondsADay = 86_400_000;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

    private readonly ILogger _logger;
    private readonly IDataAccess<BulletinMessageEntity> _table;
    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly IItemSerialPool _serials;
    private readonly ITimerService _timers;
    private readonly BulletinBoardsConfig _config;
    private readonly TimeProvider _time;
    private readonly IPacketSendService _sender;

    private readonly Dictionary<Serial, BulletinMessageEntity> _messages = new();

    // The messages removed since the last world save, which deletes their rows.
    private readonly ConcurrentDictionary<Serial, byte> _removed = new();

    // When each character last started a thread and last posted on each board. Kept apart from the messages: one
    // who removes its own post still waits.
    private readonly Dictionary<(Serial Board, Serial Poster), long> _lastThread = new();
    private readonly Dictionary<(Serial Board, Serial Poster), long> _lastPost = new();

    private string? _timerId;

    public IReadOnlyCollection<BulletinMessageEntity> Messages => _messages.Values;

    /// <summary>
    ///     Gets how many waits between posts are remembered; the hourly sweep forgets those that are over.
    /// </summary>
    public int Waits => _lastPost.Count + _lastThread.Count;

    public BulletinBoardService(
        IDataAccess<BulletinMessageEntity> table,
        IItemService items,
        IItemTemplateService templates,
        IItemSerialPool serials,
        ITimerService timers,
        BulletinBoardsConfig config,
        TimeProvider time,
        IPacketSendService sender,
        ILogger? logger = null
    )
    {
        _logger = logger ?? Log.ForContext<BulletinBoardService>();
        _table = table;
        _items = items;
        _templates = templates;
        _serials = serials;
        _timers = timers;
        _config = config;
        _time = time;
        _sender = sender;
    }

    public async Task StartAsync()
    {
        foreach (var message in await _table.GetAllAsync())
        {
            _messages[message.Id] = message;
        }

        // A reply whose thread is not there has no place in the list of its board.
        foreach (var orphan in _messages.Values
                     .Where(message => !message.IsThread && !_messages.ContainsKey(message.ThreadId))
                     .ToArray())
        {
            Forget(orphan);
        }

        _timerId = _timers.RegisterTimer(TimerName, SweepInterval, Sweep, SweepInterval, true);
        _logger.Information("Loaded {Count} bulletin board messages", _messages.Count);
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public bool IsBoard(ItemEntity item)
    {
        return _templates.TryGet(item.TemplateId, out var template) && template.ScriptId == ScriptId;
    }

    public bool Open(ItemEntity board, GameSession session)
    {
        Expire(board.Id);

        var name = string.IsNullOrWhiteSpace(board.Name) ? DefaultName : board.Name;

        // The board first, then its messages as the items of a container: the client asks for each summary itself.
        return _sender.TrySend(session.SessionId, new BulletinBoardDisplayPacket(board.Id, name)) &&
               _sender.TrySend(
                   session.SessionId,
                   new ContainerContentPacket(GetMessages(board.Id).Select(AsItem), session.UsesContainerGrid())
               );
    }

    /// <summary>
    ///     Gets the message as the item the client lists it as: a note inside its board.
    /// </summary>
    public static ItemEntity AsItem(BulletinMessageEntity message)
    {
        return new()
        {
            Id = message.Id, ItemId = MessageItemId, Amount = 1, ContainerId = message.BoardId, GridX = 0, GridY = 0
        };
    }

    public IReadOnlyList<BulletinMessageEntity> GetMessages(Serial board)
    {
        var mine = _messages.Values.Where(message => message.BoardId == board).ToArray();
        var listed = new List<BulletinMessageEntity>(mine.Length);

        foreach (var thread in mine.Where(message => message.IsThread)
                     .OrderBy(message => message.PostedAt)
                     .ThenBy(message => message.Id.Value))
        {
            listed.Add(thread);
            listed.AddRange(
                mine.Where(message => message.ThreadId == thread.Id)
                    .OrderBy(message => message.PostedAt)
                    .ThenBy(message => message.Id.Value)
            );
        }

        return listed;
    }

    public BulletinMessageEntity? GetMessage(Serial message)
    {
        return _messages.GetValueOrDefault(message);
    }

    public BulletinPostResult Post(
        ItemEntity board,
        MobileEntity poster,
        AccountType rank,
        Serial replyTo,
        string subject,
        IReadOnlyList<string> lines
    )
    {
        if (!TryClean(subject, lines, out var title, out var text))
        {
            return new() { Type = BulletinPostResultType.Empty };
        }

        var thread = ThreadOf(board.Id, replyTo);
        var now = Now();

        if (rank < AccountType.GameMaster && Wait(board.Id, poster.Id, thread is null, now) is var wait and > 0)
        {
            return new() { Type = BulletinPostResultType.TooSoon, WaitSeconds = wait };
        }

        var message = new BulletinMessageEntity
        {
            PosterId = poster.Id,
            PosterName = poster.Name,
            PosterBody = poster.Body,
            PosterHue = poster.SkinHue.Value,
            PosterEquipment = BulletinEquipment.Format(Worn(poster))
        };
        var result = Put(board.Id, thread, message, title, text, now);

        if (result.Type == BulletinPostResultType.Ok)
        {
            if (thread is null)
            {
                _lastThread[(board.Id, poster.Id)] = now;
            }

            _lastPost[(board.Id, poster.Id)] = now;
        }

        return result;
    }

    public BulletinPostResult PostAs(
        ItemEntity board,
        string name,
        string subject,
        IReadOnlyList<string> lines,
        Serial replyTo = default
    )
    {
        var poster = Clean(name, MaxName);

        if (poster.Length == 0 || !TryClean(subject, lines, out var title, out var text))
        {
            return new() { Type = BulletinPostResultType.Empty };
        }

        // No character posted it: nobody waits, and the client draws a bare body beside the message.
        var message = new BulletinMessageEntity { PosterName = poster, PosterBody = ScriptBody };

        return Put(board.Id, ThreadOf(board.Id, replyTo), message, title, text, Now());
    }

    // The subject and the lines as they are kept; false when there is no subject or no line of text.
    private static bool TryClean(string subject, IReadOnlyList<string> lines, out string title, out List<string> text)
    {
        title = Clean(subject, MaxSubject);
        text = lines.Take(MaxLines).Select(line => Clean(line, MaxLine)).ToList();

        // The empty lines a client leaves under the text are not part of it.
        while (text.Count > 0 && text[^1].Length == 0)
        {
            text.RemoveAt(text.Count - 1);
        }

        return title.Length > 0 && text.Count > 0;
    }

    // The message goes on the board with a serial of its own, under its thread, and the board is brought back to
    // its size.
    private BulletinPostResult Put(
        Serial board,
        BulletinMessageEntity? thread,
        BulletinMessageEntity message,
        string title,
        List<string> text,
        long now
    )
    {
        if (!_serials.TryTake(out var serial))
        {
            return new() { Type = BulletinPostResultType.Busy };
        }

        message.Id = serial;
        message.BoardId = board;
        message.ThreadId = thread?.Id ?? Serial.Zero;
        message.Subject = title;
        message.Body = string.Join('\n', text);
        message.PostedAt = now;
        message.LastReplyAt = now;
        _messages[serial] = message;
        _removed.TryRemove(serial, out _);

        if (thread is not null)
        {
            thread.LastReplyAt = now;
        }

        return new() { Type = BulletinPostResultType.Ok, Message = message, Dropped = MakeRoom(board, message) };
    }

    public bool CanRemove(BulletinMessageEntity message, MobileEntity by, AccountType rank)
    {
        return rank >= AccountType.GameMaster || (message.PosterId.IsValid && message.PosterId == by.Id);
    }

    public IReadOnlyList<Serial> Remove(Serial message)
    {
        if (!_messages.TryGetValue(message, out var found))
        {
            return [];
        }

        var gone = new List<BulletinMessageEntity> { found };

        if (found.IsThread)
        {
            gone.AddRange(_messages.Values.Where(reply => reply.ThreadId == found.Id));
        }

        foreach (var each in gone)
        {
            Forget(each);
        }

        return gone.Select(each => each.Id).ToArray();
    }

    public IReadOnlyList<Serial> Expire(Serial board)
    {
        if (_config.ExpireDays == 0)
        {
            return [];
        }

        var oldest = Now() - _config.ExpireDays * MillisecondsADay;
        var gone = new List<Serial>();

        foreach (var thread in _messages.Values
                     .Where(message => message.BoardId == board && message.IsThread && message.LastReplyAt < oldest)
                     .ToArray())
        {
            gone.AddRange(Remove(thread.Id));
        }

        return gone;
    }

    // A timer callback that throws closes the timer wheel: one bad board must not stop the server.
    public void Sweep()
    {
        ForgetOldWaits();

        foreach (var board in _messages.Values.Select(message => message.BoardId).Distinct().ToArray())
        {
            try
            {
                if (_items.TryGet(board, out _))
                {
                    Expire(board);

                    continue;
                }

                // The board item was deleted: nobody can read what was on it.
                foreach (var message in _messages.Values.Where(message => message.BoardId == board).ToArray())
                {
                    Forget(message);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The sweep of bulletin board {Board} failed", board);
            }
        }
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _removed.Keys.ToArray();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _removed.TryRemove(serial, out _);
        }
    }

    private void Forget(BulletinMessageEntity message)
    {
        _messages.Remove(message.Id);
        _removed[message.Id] = 0;
    }

    // The first message of the thread a reply goes under; null for a new thread, and for a reply to a message that
    // is gone or of another board.
    private BulletinMessageEntity? ThreadOf(Serial board, Serial replyTo)
    {
        if (!replyTo.IsValid || !_messages.TryGetValue(replyTo, out var parent) || parent.BoardId != board)
        {
            return null;
        }

        return parent.IsThread ? parent : _messages.GetValueOrDefault(parent.ThreadId);
    }

    // The seconds the poster still waits on this board: for a new thread since its last new thread there, for a
    // reply since its last post of any kind there. Never longer than the setting, whatever the clock did.
    private int Wait(Serial board, Serial poster, bool newThread, long now)
    {
        var (times, seconds) = newThread ? (_lastThread, _config.ThreadSeconds) : (_lastPost, _config.ReplySeconds);

        if (!times.TryGetValue((board, poster), out var last))
        {
            return 0;
        }

        var left = Math.Min(seconds * 1000L - (now - last), seconds * 1000L);

        return left <= 0 ? 0 : (int)((left + 999) / 1000);
    }

    // The waits nobody can still be held by: older than the longest wait the settings allow.
    private void ForgetOldWaits()
    {
        var oldest = Now() - BulletinBoardsConfig.MaximumWaitSeconds * 1000L;

        foreach (var times in new[] { _lastThread, _lastPost })
        {
            foreach (var key in times.Where(entry => entry.Value < oldest).Select(entry => entry.Key).ToArray())
            {
                times.Remove(key);
            }
        }
    }

    // A board beyond its size lets go its thread left longest without a reply, whole; never the one just posted to,
    // which gives up its oldest replies instead.
    private List<Serial> MakeRoom(Serial board, BulletinMessageEntity posted)
    {
        var dropped = new List<Serial>();
        var keep = posted.IsThread ? posted.Id : posted.ThreadId;

        while (_messages.Values.Count(message => message.BoardId == board) > _config.MaxMessages)
        {
            var oldest = _messages.Values
                             .Where(message => message.BoardId == board && message.IsThread && message.Id != keep)
                             .OrderBy(message => message.LastReplyAt)
                             .ThenBy(message => message.Id.Value)
                             .FirstOrDefault()
                         ?? _messages.Values
                             .Where(message => message.ThreadId == keep && message.Id != posted.Id)
                             .OrderBy(message => message.PostedAt)
                             .ThenBy(message => message.Id.Value)
                             .FirstOrDefault();

            if (oldest is null)
            {
                break;
            }

            dropped.AddRange(Remove(oldest.Id));
        }

        return dropped;
    }

    // What the poster wears, as the client draws it beside the message: the layers of a body, not its bank box.
    private IEnumerable<BulletinEquipment> Worn(MobileEntity poster)
    {
        return _items.GetWorn(poster.Id)
            .Where(item => item.Layer is >= LayerType.OneHanded and <= LayerType.Mount)
            .OrderBy(item => item.Layer)
            .Select(item => new BulletinEquipment(item.ItemId, item.Hue.Value));
    }

    // One line of plain text, no longer than the limit.
    private static string Clean(string? text, int max)
    {
        var line = new string((text ?? "").Where(letter => !char.IsControl(letter)).ToArray()).Trim();

        if (line.Length <= max)
        {
            return line;
        }

        // A character of two code units is not cut in half.
        var length = char.IsHighSurrogate(line[max - 1]) ? max - 1 : max;

        return line[..length].TrimEnd();
    }

    private long Now()
    {
        return _time.GetUtcNow().ToUnixTimeMilliseconds();
    }
}
