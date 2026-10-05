using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Internal.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Books;
using Moongate.Tests.TestSupport.Ultima.Books;
using Moongate.Ultima.Types;
namespace Moongate.Tests.Server.Ultima.Services.Books;
public sealed class BookAttachmentServiceTests
{
    [Fact]
    public async Task Claim_UnsafeOwnerApplicationPoisonsBarrierWhenLoopStops()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.Block = true;
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.World.Network.Loop.PostAsync(new Moongate.Tests.Support.GameLoop.ActionGameLoopWorkItem(() => throw new ApplicationException("Owner loop lost.")));
        await Assert.ThrowsAsync<ApplicationException>(() => f.Books.World.Network.Loop.Completion);
        f.Store.Continue.SetResult();
        await Assert.ThrowsAsync<ApplicationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<ApplicationException>(() => f.Barrier.ExecuteAsync(_ => Task.CompletedTask));
    }

    [Fact]
    public async Task Claim_GridCapacityAndRestartReceiptCacheRefuseWithoutAllocating()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        await f.Books.OnLoopAsync(() =>
        {
            Assert.True(f.Books.ItemTemplates.TryGet("backpack", out var pack));
            pack.MaxItems = 1;
        });
        Assert.Equal(BookAttachmentClaimResultType.NoCapacity, await await f.BeginAsync());
        Assert.Equal(2, f.Books.Serials.Serials.Count);
        f.Store.ClaimedIds.Add(f.Letter.Id);
        await f.Service.StartAsync();
        Assert.Equal(BookAttachmentClaimResultType.Unavailable, await await f.BeginAsync());
    }

    [Fact]
    public async Task Claim_AdmissionFailureReleasesReservation()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        await f.Barrier.CloseAsync();
        Assert.Equal(BookAttachmentClaimResultType.Failed, await await f.BeginAsync());
        Assert.Null(f.Store.Claim);
        await f.Books.OnLoopAsync(() => Assert.False(f.Reservations.IsReserved(f.Books.Player.Id)));
    }

    [Fact]
    public async Task Claim_ReplacementSessionReceivesNoOldFeedback()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.Block = true;
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.OnLoopAsync(() => f.Books.World.Sessions.Remove(f.Books.Session.SessionId));
        var replacement = await f.Books.World.AddAsync(99, entered: false);
        await f.Books.OnLoopAsync(() =>
        {
            replacement.Set(Moongate.Server.Core.Data.Sessions.SessionKeys.CharacterId, f.Books.Player.Id);
            f.Books.World.Sender.Sent.Clear();
            f.Books.World.Sender.SentSessionIds.Clear();
        });
        f.Store.Continue.SetResult();
        Assert.Equal(BookAttachmentClaimResultType.Claimed, await pending);
        Assert.Empty(f.Books.World.Sender.Sent);
    }

    [Fact]
    public async Task Claim_QueuedAdmissionRechecksTheOriginalSession()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prior = f.Barrier.ExecuteAsync(_ => gate.Task);
        var pending = await f.BeginAsync();
        await f.Books.OnLoopAsync(() => f.Books.World.Sessions.Remove(f.Books.Session.SessionId));
        gate.SetResult();
        await prior;
        Assert.Equal(BookAttachmentClaimResultType.Unavailable, await pending);
        Assert.Null(f.Store.Claim);
        await f.Books.OnLoopAsync(() => Assert.False(f.Reservations.IsReserved(f.Books.Player.Id)));
    }

    [Theory]
    [InlineData("own", true)]
    [InlineData("nested", true)]
    [InlineData("ground", false)]
    [InlineData("bank", false)]
    [InlineData("other", false)]
    [InlineData("held", false)]
    [InlineData("held-parent", false)]
    [InlineData("missing-parent", false)]
    [InlineData("cycle", false)]
    [InlineData("stack", false)]
    [InlineData("unsupported", false)]
    [InlineData("replaced", false)]
    [InlineData("malformed", false)]
    [InlineData("wrong-kind", false)]
    [InlineData("missing-reward", false)]
    [InlineData("incompatible-reward", false)]
    public async Task CanClaim_RequiresCurrentOwnBackpackAndValidIssuedPayload(string state, bool expected)
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        await f.Books.OnLoopAsync(() =>
        {
            var letter = f.Letter;
            if (state == "ground") f.Books.Items.PlaceOnGround(letter, MapType.Trammel, new(1600, 1600, 0));
            if (state == "nested")
            {
                var bag = new ItemEntity { Id = new(0x40000008) };
                bag.PutInContainer(f.Books.Backpack.Id, new Point2D(1, 1));
                f.Books.Items.Add([bag]);
                f.Books.Items.MoveToContainer(letter, bag.Id, new(1, 1));
            }
            if (state is "bank" or "other")
            {
                var pack = new ItemEntity { Id = new(0x40000008) };
                pack.Equip(state == "other" ? f.Books.Other.Id : f.Books.Player.Id, state == "bank" ? LayerType.Bank : LayerType.Backpack);
                f.Books.Items.Add([pack]);
                f.Books.Items.MoveToContainer(letter, pack.Id, new(1, 1));
            }
            if (state == "held") f.Books.Session.Set(ItemSessionKeys.Held, new HeldItem(letter.Id));
            if (state == "held-parent") f.Books.Session.Set(ItemSessionKeys.Held, new HeldItem(f.Books.Backpack.Id));
            if (state == "cycle") letter.ContainerId = letter.Id;
            if (state == "missing-parent") letter.PutInContainer(new(0x40009999), new(1, 1));
            if (state == "stack") letter.Amount = 2;
            if (state == "unsupported") letter.TemplateId = "unrelated";
            if (state == "replaced") f.Books.Items.Add([letter.Snapshot()]);
            if (state == "malformed") letter.SetProp("book.attachments", "{}");
            if (state == "wrong-kind") letter.Props!["book.attachments"] = new object();
            if (state == "missing-reward") letter.SetProp("book.attachments", letter.GetProp<string>("book.attachments").Replace("gold", "missing"));
            if (state == "incompatible-reward")
            {
                Assert.True(f.Books.ItemTemplates.TryGet("gold", out var template));
                template.Stackable = false;
            }
            Assert.Equal(expected, f.Service.CanClaim(letter, f.Books.Session));
        });
        Assert.False(f.Service.CanClaim(f.Letter, f.Books.Session));
    }
    [Fact]
    public async Task Claim_PublishesOneFrozenBatchAndNeverClaimsTwice()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        Assert.Equal(BookAttachmentClaimResultType.Claimed, await await f.BeginAsync());
        await f.Books.OnLoopAsync(() =>
        {
            Assert.Equal(100, Assert.Single(f.Books.Items.GetContents(f.Books.Backpack.Id), i => i.TemplateId == "gold").Amount);
            Assert.False(f.Service.CanClaim(f.Letter, f.Books.Session));
            Assert.False(f.Reservations.IsReserved(f.Books.Player.Id));
        });
        Assert.Equal(BookAttachmentClaimResultType.Unavailable, await await f.BeginAsync());
    }
    [Fact]
    public async Task Claim_CapacityIsCheckedForEntireBatchBeforeSerialsOrPersistence()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        await f.Books.OnLoopAsync(() => f.SetRewards(250, 250));
        Assert.Equal(BookAttachmentClaimResultType.NoCapacity, await await f.BeginAsync());
        Assert.Null(f.Store.Claim);
        Assert.Equal(2, f.Books.Serials.Serials.Count);
    }
    [Fact]
    public async Task Claim_SerialExhaustionCreatesNothing()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        await f.Books.OnLoopAsync(() => { f.SetRewards(1, 1); f.Books.Serials.Serials.Clear(); f.Books.Serials.Serials.Enqueue(new(0x40001000)); });
        Assert.Equal(BookAttachmentClaimResultType.Failed, await await f.BeginAsync());
        Assert.Null(f.Store.Claim);
        await f.Books.OnLoopAsync(() => Assert.Equal(2, f.Books.Items.Items.Count));
    }
    [Fact]
    public async Task Claim_InventoryMutationWhileCommittingIsRefused()
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.Block = true;
        var pending = await f.BeginAsync();
        await f.Store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Books.OnLoopAsync(() =>
        {
            Assert.True(f.Reservations.IsReserved(f.Books.Player.Id));
            Assert.Equal(2, f.Books.Items.Items.Count);
            Assert.False(f.Service.CanClaim(f.Letter, f.Books.Session));
            Assert.False(f.Books.Handling.Consume(f.Letter));
            Assert.False(f.Books.Handling.Delete(f.Letter));
            Assert.Null(f.Books.Handling.Give(f.Books.Player, "readable_scroll"));
            Assert.False(f.Books.Bank.Open(f.Books.Player));
            var world = f.Books.World;
            var view = new Moongate.Tests.TestSupport.Ultima.World.RecordingWorldViewService();
            var tooltips = Moongate.Tests.TestSupport.Ultima.Tooltips.TestTooltips.Create(f.Books.Items, world.Mobiles);
            var tiles = new Moongate.Tests.TestSupport.Ultima.Tiles.FakeTileDataService();
            var scripts = new Moongate.Tests.TestSupport.Ultima.Items.RecordingItemScriptService();
            var lift = new Moongate.Server.Ultima.Handlers.Items.LiftRequestPacketHandler(f.Books.Items, world.Mobiles,
                view, f.Books.Serials, tiles, world.Sender, tooltips, scripts, inventory: f.Books.Inventory);
            lift.Handle(f.Books.Session, new() { Item = f.Letter.Id, Amount = 1 });
            Assert.Null(f.Books.Session.Get(ItemSessionKeys.Held));
            var held = new HeldItem(f.Letter.Id);
            f.Books.Session.Set(ItemSessionKeys.Held, held);
            var drop = new Moongate.Server.Ultima.Handlers.Items.DropRequestPacketHandler(f.Books.Items, world.Mobiles,
                view, tiles, null!, world.Sender, tooltips, scripts, inventory: f.Books.Inventory);
            drop.Handle(f.Books.Session, new() { Item = f.Letter.Id, Destination = new(uint.MaxValue), X = 1600, Y = 1600, Z = 0, GridIndex = 0 });
            var equip = new Moongate.Server.Ultima.Handlers.Items.EquipRequestPacketHandler(f.Books.Items, world.Mobiles,
                new Moongate.Server.Ultima.Services.EquipmentService(f.Books.ItemTemplates, tiles, f.Books.Items, f.Books.Inventory),
                view, world.Sender, tooltips, scripts, f.Books.Inventory);
            equip.Handle(f.Books.Session, new() { Item = f.Letter.Id, Mobile = f.Books.Player.Id, Layer = LayerType.Shirt });
            Assert.Same(held, f.Books.Session.Get(ItemSessionKeys.Held));
            Assert.Empty(scripts.Calls);
            f.Books.Session.Set(ItemSessionKeys.Held, null);
            tiles.Item(f.Books.Backpack.ItemId, TileFlagType.Container, 0);
            var loot = new Moongate.Server.Ultima.Services.LootService(
                new Moongate.Tests.TestSupport.Ultima.Loaders.StubDataLoaderService().With(
                    new Moongate.Server.Ultima.Data.Templates.Items.LootTemplate { Id = "gift", Entries = [new() { ItemId = "gold" }] }),
                new Moongate.Tests.TestSupport.Ultima.Items.FakeItemFactoryService(f.Books.ItemTemplates, tiles), f.Books.ItemTemplates, tiles);
            var module = new Moongate.Server.Ultima.Modules.ItemModule(f.Books.Items, world.Sessions, world.Sender,
                view, tooltips, world.Mobiles, new Moongate.Tests.TestSupport.Ultima.Speech.RecordingSpeechService(),
                world.Sectors, f.Books.Handling, serials: f.Books.Serials, tiles: tiles, templates: f.Books.ItemTemplates, loot: loot, inventory: f.Books.Inventory);
            Assert.Equal(0, module.AddLoot(f.Books.Backpack.Id.Value, "gift"));
            Assert.False(module.SetProp(f.Letter.Id.Value, "test", true));
            Assert.False(module.SetName(f.Letter.Id.Value, "Changed"));
            Assert.False(module.SetHue(f.Letter.Id.Value, 20));
            Assert.False(module.SetAmount(f.Letter.Id.Value, 2));
            Assert.False(module.MoveInto(f.Letter.Id.Value, f.Books.Backpack.Id.Value));
        });
        Assert.False(pending.IsCompleted);
        Assert.Equal(BookAttachmentClaimResultType.Busy, await await f.BeginAsync());
        var save = f.Barrier.ExecuteAsync(_ => Task.CompletedTask);
        var stop = f.Service.StopAsync();
        Assert.False(save.IsCompleted);
        Assert.False(stop.IsCompleted);
        f.Store.Continue.SetResult();
        Assert.Equal(BookAttachmentClaimResultType.Claimed, await pending);
        await save;
        await stop;
    }
    [Theory]
    [InlineData((int)BookAttachmentCommitState.Committed, BookAttachmentClaimResultType.Claimed)]
    [InlineData((int)BookAttachmentCommitState.RolledBack, BookAttachmentClaimResultType.Failed)]
    [InlineData((int)BookAttachmentCommitState.AlreadyClaimed, BookAttachmentClaimResultType.Unavailable)]
    public async Task Claim_LostCommitAcknowledgementReconcilesExactlyOnce(int reconciledValue, BookAttachmentClaimResultType expected)
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.ThrowCommit = true;
        var reconciled = (BookAttachmentCommitState)reconciledValue;
        f.Store.Reconciled = reconciled;
        Assert.Equal(expected, await await f.BeginAsync());
        await f.Books.OnLoopAsync(() => Assert.Equal(reconciled == BookAttachmentCommitState.Committed ? 3 : 2, f.Books.Items.Items.Count));
        await f.Barrier.ExecuteAsync(_ => Task.CompletedTask);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_UnsafeApplicationOrReconciliationPoisonsBarrier(bool readFails)
    {
        await using var f = await BookAttachmentTestFixture.CreateAsync();
        f.Store.ThrowCommit = true;
        f.Store.ThrowReconcile = readFails;
        f.Store.Reconciled = BookAttachmentCommitState.Uncertain;
        await Assert.ThrowsAnyAsync<Exception>(async () => await await f.BeginAsync());
        await Assert.ThrowsAnyAsync<Exception>(() => f.Barrier.ExecuteAsync(_ => Task.CompletedTask));
        await f.Books.OnLoopAsync(() => Assert.True(f.Reservations.IsReserved(f.Books.Player.Id)));
    }
}
