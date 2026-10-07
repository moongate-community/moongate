using DryIoc;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Books;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Books;

namespace Moongate.Tests.Integration.Server.Ultima.Books;

[Collection(PostgresTestCollection.Name)]
public sealed class BookAttachmentServicePersistenceTests
{
    [Fact]
    public async Task Claim_MovedLetterSurvivesAncestorDeletionWithoutLosingReceipt()
    {
        await using var books = await BookTestFixture.CreateAsync();
        await using var db = await BookAttachmentStoreFixture.CreateAsync(books);
        await using var f = await BookAttachmentTestFixture.CreateAsync(db.Store, books);
        await db.Host.Container.Resolve<IDataAccess<MobileEntity>>().UpsertAsync(books.Player.Snapshot());
        Assert.Equal(BookAttachmentClaimResultType.Claimed, await await f.BeginAsync());
        var receipt = Assert.Single(await db.Receipts.GetAllAsync());
        var chest = new ItemEntity { Id = new(0x40006000), TemplateId = "backpack", ItemId = 0xE75 };
        var saves = new WorldSaveService(
            db.Host.Owner,
            books.World.Network.Loop,
            new RecordingTimerService(),
            new() { Enabled = false },
            TimeProvider.System,
            f.Barrier
        );
        await saves.StartAsync();
        saves.Activate();
        try
        {
            await books.OnLoopAsync(() =>
                {
                    books.Items.Add([chest]);
                    books.Items.PlaceOnGround(chest, books.Player.Map, books.Player.Location);
                    books.Items.MoveToContainer(f.Letter, chest.Id, new(10, 10), 0);
                }
            );
            await saves.SaveAsync();
            await books.OnLoopAsync(() =>
                {
                    books.Items.MoveToContainer(f.Letter, books.Backpack.Id, new(10, 10), 0);
                    Assert.True(books.Handling.Delete(chest));
                }
            );
            await saves.SaveAsync();
            Assert.Null(await db.Items.GetByIdAsync(chest.Id));
            Assert.Equal(books.Backpack.Id, (await db.Items.GetByIdAsync(f.Letter.Id))!.ContainerId);
            var preserved = Assert.Single(await db.Receipts.GetAllAsync());
            Assert.Equal(
                (receipt.Id, receipt.ClaimantId, receipt.ClaimedAt),
                (preserved.Id, preserved.ClaimantId, preserved.ClaimedAt)
            );
            var tiles = new Moongate.Tests.TestSupport.Ultima.Tiles.FakeTileDataService();
            var restarted = new Moongate.Server.Ultima.Services.Internal.Books.BookAttachmentService(
                books.Items,
                books.World.Mobiles,
                books.World.Sessions,
                books.ItemTemplates,
                tiles,
                books.Handling,
                new Moongate.Server.Ultima.Services.WeightService(books.Items, books.ItemTemplates, tiles),
                books.Serials,
                books.World.Network.Loop,
                books.Reservations,
                f.Barrier,
                new Moongate.Server.Ultima.Services.ContainerCapacityService(books.Items, books.ItemTemplates, new()),
                db.Store
            );
            await restarted.StartAsync();
            await books.OnLoopAsync(() => Assert.False(restarted.CanClaim(f.Letter, books.Session)));
            Task<BookAttachmentClaimResultType> retry = null!;
            await books.OnLoopAsync(() => retry = restarted.ClaimAsync(f.Letter.Id, books.Session));
            Assert.Equal(BookAttachmentClaimResultType.Unavailable, await retry);
            Assert.Single(await db.Items.GetAllAsync(), item => item.TemplateId == "gold");
            await books.OnLoopAsync(() => Assert.True(books.Handling.Delete(f.Letter)));
            await saves.SaveAsync();
            Assert.Null(await db.Items.GetByIdAsync(f.Letter.Id));
            Assert.Empty(await db.Receipts.GetAllAsync());
            await restarted.StopAsync();
        }
        finally
        {
            await saves.StopAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_LostCommitAcknowledgementReconcilesExactlyOnce(bool loseAcknowledgement)
    {
        await using var books = await BookTestFixture.CreateAsync();
        await using var db = await BookAttachmentStoreFixture.CreateAsync(books);
        var controlled = new ControlledBookAttachmentStore { Inner = db.Store, ThrowCommit = loseAcknowledgement };
        await using var f = await BookAttachmentTestFixture.CreateAsync(controlled, books);
        await db.Host.Container.Resolve<IDataAccess<MobileEntity>>().UpsertAsync(books.Player.Snapshot());
        Assert.Equal(BookAttachmentClaimResultType.Claimed, await await f.BeginAsync());
        Assert.Single(await db.Receipts.GetAllAsync());
        Assert.Equal(100, Assert.Single(await db.Items.GetAllAsync(), item => item.TemplateId == "gold").Amount);
        Assert.Equal(BookAttachmentClaimResultType.Unavailable, await await f.BeginAsync());
        Assert.Equal(3, (await db.Items.GetAllAsync()).Count);
        Assert.Contains(f.Letter.Id, await db.Store.LoadClaimedIdsAsync());
    }

    [Fact]
    public Task Claim_WorldSaveWaitsUntilLoopApplication()
    {
        return VerifySaveWaitAsync(false);
    }

    [Fact]
    public Task Claim_ShutdownDrainsBeforeFinalCapture()
    {
        return VerifySaveWaitAsync(true);
    }

    [Fact]
    public async Task Claim_UnsafeReconciliationPreventsFinalWorldCapture()
    {
        await using var books = await BookTestFixture.CreateAsync();
        await using var db = await BookAttachmentStoreFixture.CreateAsync(books);
        var controlled = new ControlledBookAttachmentStore { Inner = db.Store, ThrowCommit = true, ThrowReconcile = true };
        await using var f = await BookAttachmentTestFixture.CreateAsync(controlled, books);
        await db.Host.Container.Resolve<IDataAccess<MobileEntity>>().UpsertAsync(books.Player.Snapshot());
        var saves = new WorldSaveService(
            db.Host.Owner,
            books.World.Network.Loop,
            new RecordingTimerService(),
            new() { Enabled = false },
            TimeProvider.System,
            f.Barrier
        );
        await saves.StartAsync();
        saves.Activate();
        await Assert.ThrowsAsync<IOException>(async () => await await f.BeginAsync());
        await Assert.ThrowsAsync<IOException>(() => saves.SaveAsync());
        await Assert.ThrowsAsync<IOException>(() => saves.StopAsync(true));
        Assert.Equal(0, db.Captures);
        Assert.Single(await db.Receipts.GetAllAsync());
        Assert.Equal(3, (await db.Items.GetAllAsync()).Count);
    }

    private async Task VerifySaveWaitAsync(bool finalCapture)
    {
        await using var books = await BookTestFixture.CreateAsync();
        await using var db = await BookAttachmentStoreFixture.CreateAsync(books);
        var controlled = new ControlledBookAttachmentStore { Inner = db.Store, Block = true };
        await using var f = await BookAttachmentTestFixture.CreateAsync(controlled, books);
        await db.Host.Container.Resolve<IDataAccess<MobileEntity>>().UpsertAsync(books.Player.Snapshot());
        var saves = new WorldSaveService(
            db.Host.Owner,
            books.World.Network.Loop,
            new RecordingTimerService(),
            new() { Enabled = false },
            TimeProvider.System,
            f.Barrier
        );
        await saves.StartAsync();
        saves.Activate();
        var claim = await f.BeginAsync();
        await controlled.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await books.OnLoopAsync(() => Assert.Equal(2, books.Items.Items.Count));
        var save = finalCapture ? saves.StopAsync(true) : saves.SaveAsync();
        Assert.False(save.IsCompleted);
        Assert.Equal(0, db.Captures);
        controlled.Continue.SetResult();
        Assert.Equal(BookAttachmentClaimResultType.Claimed, await claim.WaitAsync(TimeSpan.FromSeconds(10)));
        await save.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(1, db.Captures);
        Assert.Single(await db.Receipts.GetAllAsync());
        Assert.Equal(100, Assert.Single(await db.Items.GetAllAsync(), item => item.TemplateId == "gold").Amount);
        if (!finalCapture) await saves.StopAsync();
    }
}
