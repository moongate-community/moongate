using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     A jail sentence: who is in which cell until when, and where it goes back.
/// </summary>
/// <remarks>
///     The summary is the comment of the table: one line, as the migration writes it.
/// </remarks>
[Table(Name = "world.jail_sentences")]
public class JailSentenceEntity : IMoongateEntity
{
    /// <summary>
    ///     The serial of the prisoner: a mobile has one sentence at most.
    /// </summary>
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The name of the prisoner when it was jailed.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Whether the prisoner is the character of a player: one that is not in the world is offline, not gone.
    /// </summary>
    public bool IsPlayer { get; set; }

    /// <summary>
    ///     The number of the cell.
    /// </summary>
    public int Cell { get; set; }

    /// <summary>
    ///     The length of the sentence in days.
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    ///     When the sentence began, in Unix milliseconds.
    /// </summary>
    public long JailedAt { get; set; }

    /// <summary>
    ///     When the sentence ends, in Unix milliseconds.
    /// </summary>
    public long ReleaseAt { get; set; }

    /// <summary>
    ///     The map of the place of the arrest, stored as its byte value.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public MapType ReturnMap { get; set; }

    /// <summary>
    ///     The X of the place of the arrest, where the prisoner goes back.
    /// </summary>
    public int ReturnX { get; set; }

    /// <summary>
    ///     The Y of the place of the arrest.
    /// </summary>
    public int ReturnY { get; set; }

    /// <summary>
    ///     The Z of the place of the arrest.
    /// </summary>
    public int ReturnZ { get; set; }

    /// <summary>
    ///     The name of the game master who jailed.
    /// </summary>
    public string JailedBy { get; set; } = "";

    /// <summary>
    ///     Why the prisoner was jailed, as the game master typed it; empty when no reason was given.
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    ///     Whether the staff ended the sentence early: no fine and no note at the release.
    /// </summary>
    public bool Pardoned { get; set; }

    /// <summary>
    ///     Whether the sentence waits for its prisoner, a player who was offline, to log in: its days start then.
    /// </summary>
    [Column(IsNullable = false, DbType = "boolean NOT NULL DEFAULT false")]
    public bool Pending { get; set; }

    /// <summary>
    ///     Gets whether the sentence has ended at <paramref name="now" />, in Unix milliseconds. One that waits for
    ///     its prisoner has not started, and never has.
    /// </summary>
    public bool IsOver(long now)
    {
        return !Pending && now >= ReleaseAt;
    }

    /// <summary>
    ///     Gets a detached copy to save: the live one keeps changing on the game loop while the copy is written.
    /// </summary>
    public JailSentenceEntity Snapshot()
    {
        return (JailSentenceEntity)MemberwiseClone();
    }
}
