using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Types.Help;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     A request for a game master: who asked, what about, where, and how the staff dealt with it.
/// </summary>
/// <remarks>
///     The summary is the comment of the table: one line, as the migration writes it.
/// </remarks>
[Table(Name = "world.help_pages")]
public class HelpPageEntity : IMoongateEntity
{
    /// <summary>
    ///     The number of the request, shown to the staff.
    /// </summary>
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The serial of the character who asked.
    /// </summary>
    [Column(MapType = typeof(long))]
    public Serial Player { get; set; }

    /// <summary>
    ///     The name of the character when it asked.
    /// </summary>
    public string PlayerName { get; set; } = "";

    /// <summary>
    ///     The serial of the account of the character.
    /// </summary>
    [Column(MapType = typeof(long))]
    public Serial AccountId { get; set; }

    /// <summary>
    ///     What it is about, stored as the byte value of HelpPageKindType: 0 question, 1 bug, 2 suggestion, 3 harassment.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public HelpPageKindType Kind { get; set; }

    /// <summary>
    ///     The line the player typed, at most 128 characters.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    ///     The map where the player stood, stored as its byte value.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public MapType Map { get; set; }

    /// <summary>
    ///     The X where the player stood.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     The Y where the player stood.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     The Z where the player stood.
    /// </summary>
    public int Z { get; set; }

    /// <summary>
    ///     Stored as the byte value of HelpPageStatusType: 0 open, 1 taken, 2 closed.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public HelpPageStatusType Status { get; set; }

    /// <summary>
    ///     The name of the game master who took or closed it; empty when nobody did.
    /// </summary>
    public string TakenBy { get; set; } = "";

    /// <summary>
    ///     The answer of the game master, at most 128 characters; empty when it was closed without one.
    /// </summary>
    public string Answer { get; set; } = "";

    /// <summary>
    ///     Whether the player has read the answer; true for a request closed without one.
    /// </summary>
    [Column(IsNullable = false, DbType = "boolean NOT NULL DEFAULT false")]
    public bool AnswerDelivered { get; set; }

    /// <summary>
    ///     When it was asked, in Unix milliseconds.
    /// </summary>
    public long CreatedAt { get; set; }

    /// <summary>
    ///     When it was closed, in Unix milliseconds; 0 while it is not.
    /// </summary>
    public long ClosedAt { get; set; }

    /// <summary>
    ///     Gets whether the request is still to be dealt with: open or taken.
    /// </summary>
    public bool IsActive => Status != HelpPageStatusType.Closed;

    /// <summary>
    ///     Gets a detached copy, for the world save to write while the game goes on.
    /// </summary>
    public HelpPageEntity Snapshot()
    {
        return (HelpPageEntity)MemberwiseClone();
    }
}
