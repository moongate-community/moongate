using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.BulletinBoards;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.BulletinBoards;

namespace Moongate.Tests.TestSupport.Ultima.BulletinBoards;

/// <summary>
///     Bulletin boards that record who opened which, with settable answers; every item is a board unless told.
/// </summary>
public sealed class StubBulletinBoardService : IBulletinBoardService
{
    public List<BulletinMessageEntity> MessageList { get; } = [];

    public IReadOnlyCollection<BulletinMessageEntity> Messages => MessageList;

    /// <summary>
    ///     Whether an item is a board; every item is one by default.
    /// </summary>
    public Func<ItemEntity, bool> Boards { get; set; } = _ => true;

    public bool OpenResult { get; set; } = true;

    public List<(ItemEntity Board, GameSession Session)> Opened { get; } = [];

    public bool IsBoard(ItemEntity item)
    {
        return Boards(item);
    }

    public bool Open(ItemEntity board, GameSession session)
    {
        Opened.Add((board, session));

        return OpenResult;
    }

    public IReadOnlyList<BulletinMessageEntity> GetMessages(Serial board)
    {
        return MessageList.Where(message => message.BoardId == board).ToList();
    }

    public BulletinMessageEntity? GetMessage(Serial message)
    {
        return MessageList.FirstOrDefault(each => each.Id == message);
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
        return new() { Type = BulletinPostResultType.Empty };
    }

    /// <summary>
    ///     What a script's post answers; the message it gives is added to <see cref="MessageList" />.
    /// </summary>
    public BulletinPostResultType PostAsResult { get; set; } = BulletinPostResultType.Ok;

    public List<(ItemEntity Board, string Name, string Subject, IReadOnlyList<string> Lines, Serial ReplyTo)> PostedAs
    {
        get;
    } = [];

    public BulletinPostResult PostAs(
        ItemEntity board,
        string name,
        string subject,
        IReadOnlyList<string> lines,
        Serial replyTo = default
    )
    {
        PostedAs.Add((board, name, subject, lines.ToArray(), replyTo));

        if (PostAsResult != BulletinPostResultType.Ok)
        {
            return new() { Type = PostAsResult };
        }

        var message = new BulletinMessageEntity
        {
            Id = new Serial(0x40005000 + (uint)MessageList.Count), BoardId = board.Id, ThreadId = replyTo, PosterName = name,
            Subject = subject, Body = string.Join('\n', lines)
        };
        MessageList.Add(message);

        return new() { Type = BulletinPostResultType.Ok, Message = message };
    }

    public bool CanRemove(BulletinMessageEntity message, MobileEntity by, AccountType rank)
    {
        return true;
    }

    public IReadOnlyList<Serial> Remove(Serial message)
    {
        return MessageList.RemoveAll(each => each.Id == message) > 0 ? [message] : [];
    }

    public IReadOnlyList<Serial> Expire(Serial board)
    {
        return [];
    }

    public void Sweep()
    {
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return [];
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
