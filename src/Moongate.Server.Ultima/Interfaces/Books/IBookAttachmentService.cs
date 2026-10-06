using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Books;

namespace Moongate.Server.Ultima.Interfaces.Books;

/// <summary>
///     Supervises durable, once-only delivery of a letter's frozen attachments.
/// </summary>
public interface IBookAttachmentService : IMoongateStartupService
{
    /// <summary>
    ///     Checks eligibility on the game loop using the issued payload and backpack ancestry.
    /// </summary>
    bool CanClaim(ItemEntity letter, GameSession session);

    /// <summary>
    ///     Admits a withdrawal on the loop; await settlement off-loop. Unsafe outcomes fault the persistence barrier.
    /// </summary>
    Task<BookAttachmentClaimResultType> ClaimAsync(Serial letterId, GameSession session);
}
