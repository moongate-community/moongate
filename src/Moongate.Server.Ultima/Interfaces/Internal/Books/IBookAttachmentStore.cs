using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Types.Books;

namespace Moongate.Server.Ultima.Interfaces.Internal.Books;

/// <summary>
///     Stores a detached reward batch and its receipt atomically in the world database.
/// </summary>
internal interface IBookAttachmentStore
{
    /// <summary>
    ///     Inserts every reward and the receipt in one transaction without retrying its callback.
    /// </summary>
    Task CommitAsync(BookAttachmentClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reads durable state after settlement to classify an uncertain acknowledgement.
    /// </summary>
    Task<BookAttachmentCommitState> ReconcileAsync(BookAttachmentClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads authoritative claimed letter ids for startup eligibility.
    /// </summary>
    Task<IReadOnlyCollection<Serial>> LoadClaimedIdsAsync(CancellationToken cancellationToken = default);
}
