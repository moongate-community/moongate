using FreeSql.DataAnnotations;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     A durable receipt preventing a physical letter from delivering its attachments twice.
/// </summary>
[Table(Name = "world.book_attachment_claims")]
public sealed class BookAttachmentClaimEntity : IMoongateEntity
{
    /// <summary>
    ///     The letter's existing item serial; no new identity is allocated.
    /// </summary>
    [Column(Name = "letter_id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The character who withdrew the attachments.
    /// </summary>
    [Column(MapType = typeof(long))]
    public Serial ClaimantId { get; set; }

    /// <summary>
    ///     The withdrawal time in UTC Unix milliseconds.
    /// </summary>
    public long ClaimedAt { get; set; }
}
