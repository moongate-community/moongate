using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Interfaces.Internal.Books;
using Moongate.Server.Ultima.Types.Books;

namespace Moongate.Tests.TestSupport.Ultima.Books;

internal sealed class ControlledBookAttachmentStore : IBookAttachmentStore
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IBookAttachmentStore? Inner { get; set; }
    public bool Block { get; set; }
    public bool ThrowCommit { get; set; }
    public bool ThrowReconcile { get; set; }
    public BookAttachmentCommitState Reconciled { get; set; } = BookAttachmentCommitState.Committed;
    public BookAttachmentClaim? Claim { get; private set; }
    public List<Serial> ClaimedIds { get; } = [];

    public async Task CommitAsync(BookAttachmentClaim claim, CancellationToken cancellationToken = default)
    {
        Claim = claim;
        Entered.TrySetResult();
        if (Block) await Continue.Task;
        if (Inner is not null) await Inner.CommitAsync(claim, cancellationToken);
        if (ThrowCommit) throw new IOException("Lost acknowledgement.");
    }

    public Task<BookAttachmentCommitState> ReconcileAsync(
        BookAttachmentClaim claim, CancellationToken cancellationToken = default
    )
    {
        if (ThrowReconcile) throw new IOException("Reconciliation unavailable.");
        return Inner?.ReconcileAsync(claim, cancellationToken) ?? Task.FromResult(Reconciled);
    }

    public Task<IReadOnlyCollection<Serial>> LoadClaimedIdsAsync(CancellationToken cancellationToken = default)
    {
        return Inner?.LoadClaimedIdsAsync(cancellationToken) ?? Task.FromResult<IReadOnlyCollection<Serial>>(ClaimedIds);
    }
}
