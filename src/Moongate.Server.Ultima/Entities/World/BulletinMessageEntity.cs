using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     A message on a bulletin board: who wrote what on which board, and the thread it belongs to.
/// </summary>
/// <remarks>
///     The summary is the comment of the table: one line, as the migration writes it.
/// </remarks>
[Table(Name = "world.bulletin_messages"), Index("ix_bulletin_messages_board", nameof(BoardId))]
public class BulletinMessageEntity : IMoongateEntity
{
    /// <summary>
    ///     The serial of the message, taken from the item serials: the client lists it as an item of the board.
    /// </summary>
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The serial of the board item the message is on.
    /// </summary>
    [Column(MapType = typeof(long))]
    public Serial BoardId { get; set; }

    /// <summary>
    ///     The serial of the first message of the thread; 0 on that first message.
    /// </summary>
    [Column(MapType = typeof(long))]
    public Serial ThreadId { get; set; }

    /// <summary>
    ///     The serial of the character who posted; 0 when a script did.
    /// </summary>
    [Column(MapType = typeof(long))]
    public Serial PosterId { get; set; }

    /// <summary>
    ///     The name of the poster when it posted.
    /// </summary>
    public string PosterName { get; set; } = "";

    /// <summary>
    ///     The subject, one line.
    /// </summary>
    public string Subject { get; set; } = "";

    /// <summary>
    ///     The lines of the message, joined with a line feed.
    /// </summary>
    [Column(DbType = "text")]
    public string Body { get; set; } = "";

    /// <summary>
    ///     When it was posted, in Unix milliseconds.
    /// </summary>
    public long PostedAt { get; set; }

    /// <summary>
    ///     On the first message of a thread: when it or its last reply was posted, in Unix milliseconds.
    /// </summary>
    public long LastReplyAt { get; set; }

    /// <summary>
    ///     The body of the poster when it posted.
    /// </summary>
    public int PosterBody { get; set; }

    /// <summary>
    ///     The hue of the poster when it posted.
    /// </summary>
    public int PosterHue { get; set; }

    /// <summary>
    ///     What the poster wore when it posted: itemId:hue pairs joined with commas.
    /// </summary>
    [Column(DbType = "text")]
    public string PosterEquipment { get; set; } = "";

    /// <summary>
    ///     Gets whether the message starts a thread; the others are replies to one.
    /// </summary>
    [Column(IsIgnore = true)]
    public bool IsThread => ThreadId == Serial.Zero;

    /// <summary>
    ///     Gets the lines of the message; none for an empty one.
    /// </summary>
    public IReadOnlyList<string> Lines()
    {
        return Body.Length == 0 ? [] : Body.Split('\n');
    }

    /// <summary>
    ///     Gets a detached copy to save: the live one keeps changing on the game loop while the copy is written.
    /// </summary>
    public BulletinMessageEntity Snapshot()
    {
        return (BulletinMessageEntity)MemberwiseClone();
    }
}
