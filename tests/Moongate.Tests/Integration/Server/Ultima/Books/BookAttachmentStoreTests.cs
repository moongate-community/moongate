using Moongate.Server.Ultima.Types.Books;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

[Collection(PostgresTestCollection.Name)]
public sealed class BookAttachmentStoreTests
{
    [Fact]
    public async Task Commit_UnsavedLetterAndParents_PersistsOneBatchAndReceipt()
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var claim = fixture.Claim();
        Assert.Equal(BookAttachmentCommitState.RolledBack, await fixture.Store.ReconcileAsync(claim));
        await fixture.Store.CommitAsync(claim);
        Assert.Equal(3, (await fixture.Items.GetAllAsync()).Count);
        Assert.Single(await fixture.Receipts.GetAllAsync());
        Assert.Equal(100, (await fixture.Items.GetByIdAsync(claim.Items[0].Id))!.Amount);
        Assert.Equal(BookAttachmentCommitState.Committed, await fixture.Store.ReconcileAsync(claim));
        Assert.Contains(claim.Letter.Id, await fixture.Store.LoadClaimedIdsAsync());
        Assert.Empty(await fixture.Host.Owner.PreviewSchemaAsync());
    }

    [Fact]
    public async Task Commit_DuplicateReceipt_RollsBackNewBatchAndPreservesFirstReceipt()
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var first = fixture.Claim();
        await fixture.Store.CommitAsync(first);
        var second = fixture.Claim(0x40000004);
        second.Receipt.ClaimantId = new(200);
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Store.CommitAsync(second));
        Assert.Null(await fixture.Items.GetByIdAsync(second.Items[0].Id));
        Assert.Equal(first.Receipt.ClaimantId, (await fixture.Receipts.GetByIdAsync(first.Letter.Id))!.ClaimantId);
        Assert.Equal(BookAttachmentCommitState.AlreadyClaimed, await fixture.Store.ReconcileAsync(second));
    }

    [Fact]
    public async Task Commit_LateItemFailure_RollsBackLetterParentsRewardsAndReceipt()
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var first = fixture.Claim();
        var invalid = fixture.Claim(0x40000004, 0).Items[0];
        var claim = first with { Items = [first.Items[0], invalid] };
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Store.CommitAsync(claim));
        Assert.Empty(await fixture.Items.GetAllAsync());
        Assert.Empty(await fixture.Receipts.GetAllAsync());
        Assert.Equal(BookAttachmentCommitState.RolledBack, await fixture.Store.ReconcileAsync(claim));
    }

    [Fact]
    public async Task Receipt_SaveAndDeleteLetter_StaysAuthoritativeUntilDeletion()
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var claim = fixture.Claim();
        await fixture.Store.CommitAsync(claim);
        await fixture.Items.UpsertAsync(claim.Letter.Snapshot());
        await fixture.Host.Owner.SaveAllAsync();
        Assert.Single(await fixture.Receipts.GetAllAsync());
        Assert.Equal(BookAttachmentCommitState.Committed, await fixture.Store.ReconcileAsync(claim));
        await fixture.Items.DeleteAsync(claim.Letter.Id);
        Assert.Empty(await fixture.Receipts.GetAllAsync());
    }

    [Fact]
    public async Task Reconcile_MissingOrChangedReward_FailsClosed()
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var claim = fixture.Claim();
        await fixture.Store.CommitAsync(claim);
        var reward = await fixture.Items.GetByIdAsync(claim.Items[0].Id);
        reward!.Amount = 99;
        await fixture.Items.UpsertAsync(reward);
        Assert.Equal(BookAttachmentCommitState.Uncertain, await fixture.Store.ReconcileAsync(claim));
        await fixture.Items.DeleteAsync(reward.Id);
        Assert.Equal(BookAttachmentCommitState.Uncertain, await fixture.Store.ReconcileAsync(claim));
    }
}
