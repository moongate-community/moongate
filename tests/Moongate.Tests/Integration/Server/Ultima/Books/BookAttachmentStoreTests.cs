using Moongate.Server.Ultima.Types.Books;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Ultima.Books;
using Npgsql;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

[Collection(PostgresTestCollection.Name)]
public sealed class BookAttachmentStoreTests
{
    [Fact]
    public async Task Receipt_ReferencesMissingLetter_IsRefused()
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => fixture.Host.Database.ExecuteAsync(
            "INSERT INTO world.book_attachment_claims(letter_id, claimant_id, claimed_at) VALUES (1073741900, 100, 1000)"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Empty(await fixture.Receipts.GetAllAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Receipt_ConcurrentInsertionAndLetterDeletion_LeaveNoOrphan(bool insertFirst)
    {
        await using var fixture = await BookAttachmentStoreFixture.CreateAsync();
        var claim = fixture.Claim();
        await fixture.Store.CommitAsync(claim);
        await fixture.Receipts.DeleteAsync(claim.Letter.Id);
        await using var inserter = new NpgsqlConnection(fixture.Host.Database.ConnectionString);
        await inserter.OpenAsync();
        await using var insertion = await inserter.BeginTransactionAsync();
        await using var deleter = new NpgsqlConnection(fixture.Host.Database.ConnectionString);
        await deleter.OpenAsync();
        await using var deletion = await deleter.BeginTransactionAsync();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO world.book_attachment_claims(letter_id, claimant_id, claimed_at) VALUES (@letter, 100, 1000)",
            inserter, insertion) { CommandTimeout = 10 };
        insert.Parameters.AddWithValue("letter", (long)claim.Letter.Id.Value);
        await using var delete = new NpgsqlCommand("DELETE FROM world.items WHERE id = @letter", deleter, deletion)
            { CommandTimeout = 10 };
        delete.Parameters.AddWithValue("letter", (long)claim.Letter.Id.Value);
        if (insertFirst)
        {
            await insert.ExecuteNonQueryAsync();
            var pendingDelete = delete.ExecuteNonQueryAsync();
            await WaitForDatabaseLockAsync(fixture.Host.Database, deleter.ProcessID);
            Assert.False(pendingDelete.IsCompleted);
            await insertion.CommitAsync();
            await pendingDelete;
            await deletion.CommitAsync();
        }
        else
        {
            await delete.ExecuteNonQueryAsync();
            var pendingInsert = insert.ExecuteNonQueryAsync();
            await WaitForDatabaseLockAsync(fixture.Host.Database, inserter.ProcessID);
            Assert.False(pendingInsert.IsCompleted);
            await deletion.CommitAsync();
            var exception = await Assert.ThrowsAsync<PostgresException>(() => pendingInsert);
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
            await insertion.RollbackAsync();
        }
        Assert.Null(await fixture.Items.GetByIdAsync(claim.Letter.Id));
        Assert.Empty(await fixture.Receipts.GetAllAsync());
    }

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

    private static async Task WaitForDatabaseLockAsync(PostgreSqlTestDatabase database, int processId)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (await database.ScalarAsync<long>(
            $"SELECT COUNT(*) FROM pg_stat_activity WHERE pid = {processId} AND wait_event_type = 'Lock'") == 0)
        {
            await Task.Delay(10, deadline.Token);
        }
    }
}
