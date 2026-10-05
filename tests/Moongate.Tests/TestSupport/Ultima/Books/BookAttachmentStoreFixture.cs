using DryIoc;
using Moongate.Core.Geometry;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Books;

internal sealed class BookAttachmentStoreFixture : IAsyncDisposable
{
    public HostPersistenceFixture Host { get; }
    public BookAttachmentStore Store { get; }
    public IDataAccess<ItemEntity> Items { get; }
    public IDataAccess<BookAttachmentClaimEntity> Receipts { get; }

    private BookAttachmentStoreFixture(HostPersistenceFixture host)
    {
        Host = host;
        Items = host.Container.Resolve<IDataAccess<ItemEntity>>();
        Receipts = host.Container.Resolve<IDataAccess<BookAttachmentClaimEntity>>();
        Store = new(new WorldTransactionService(host.Owner), Receipts);
    }

    public static async Task<BookAttachmentStoreFixture> CreateAsync()
    {
        var host = await HostPersistenceFixture.CreateAsync(false);
        try
        {
            host.Container.AddPersistenceWorld<MobileEntity>().AddPersistenceWorld<ItemEntity>()
                .AddPersistenceWorld<BookAttachmentClaimEntity>();
            await CoreMigrationFiles.ApplyAsync(host.Database, "world");
            await host.Owner.InitializeAsync();
            return new(host);
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    public BookAttachmentClaim Claim(uint rewardId = 0x40000003, int amount = 100)
    {
        var pack = new ItemEntity { Id = new(0x40000001), TemplateId = "backpack", ItemId = 0xE75 };
        pack.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        var letter = new ItemEntity { Id = new(0x40000002), TemplateId = "readable_scroll", ItemId = 0x14ED };
        letter.PutInContainer(pack.Id, new(10, 10), 0);
        letter.SetProp("book.content", "Welcome Pippo");
        var reward = new ItemEntity { Id = new(rewardId), TemplateId = "gold", ItemId = 0xEED, Amount = amount };
        reward.PutInContainer(pack.Id, new(20, 20), 1);
        return new() { Letter = letter, Parents = [pack], Items = [reward],
            Receipt = new() { Id = letter.Id, ClaimantId = new(100), ClaimedAt = 1000 } };
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
    }
}
