using System.Collections.Immutable;
using Moongate.Core.Primitives;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Services.Items;
using Moongate.Tests.TestSupport.Ultima.Tiles;

namespace Moongate.Tests.TestSupport.Ultima.Books;

internal sealed class BookAttachmentTestFixture : IAsyncDisposable
{
    private readonly bool _ownsBooks;
    public BookTestFixture Books { get; }
    public ControlledBookAttachmentStore Store { get; } = new();
    public PersistenceOperationBarrier Barrier { get; } = new();
    public InventoryReservationService Reservations { get; }
    public BookAttachmentService Service { get; }
    public ItemEntity Letter { get; private set; } = null!;

    private BookAttachmentTestFixture(
        BookTestFixture books, Moongate.Server.Ultima.Interfaces.Internal.Books.IBookAttachmentStore? store, bool ownsBooks
    )
    {
        _ownsBooks = ownsBooks;
        Books = books;
        Reservations = books.Reservations;
        var tiles = new FakeTileDataService();
        books.Data.With(
            books.Data.GetEntities<ItemTemplate>()
                .Where(template => template.Id != "gold")
                .Append(new ItemTemplate { Id = "gold", ItemId = new(0xEED), Stackable = true, Weight = 1m })
                .ToArray()
        );
        Service = new(
            books.Items,
            books.World.Mobiles,
            books.World.Sessions,
            books.ItemTemplates,
            tiles,
            books.Handling,
            new WeightService(books.Items, books.ItemTemplates, tiles),
            books.Serials,
            books.World.Network.Loop,
            Reservations,
            Barrier,
            new ContainerCapacityService(books.Items, books.ItemTemplates, new()),
            store ?? Store
        );
    }

    public static async Task<BookAttachmentTestFixture> CreateAsync(
        Moongate.Server.Ultima.Interfaces.Internal.Books.IBookAttachmentStore? store = null, BookTestFixture? books = null
    )
    {
        var fixture = new BookAttachmentTestFixture(books ?? await BookTestFixture.CreateAsync(), store, books is null);
        fixture.Books.RebuildDocuments(fixture.Service);
        await fixture.Service.StartAsync();
        await fixture.Books.OnLoopAsync(() =>
            {
                fixture.Letter = fixture.Books.Give();
                fixture.SetRewards(100);
                fixture.Books.Serials.Serials.Enqueue(new(0x40001000));
                fixture.Books.Serials.Serials.Enqueue(new(0x40001001));
            }
        );
        return fixture;
    }

    public void SetRewards(params int[] amounts)
    {
        Letter.SetProp(
            BookAttachmentCodec.PropKey,
            BookAttachmentCodec.Encode(
                new BookAttachmentPayload
                {
                    Items = amounts.Select(amount =>
                            BookAttachmentCodec.Freeze(
                                new ItemEntity { TemplateId = "gold", ItemId = 0xEED, Amount = amount }
                            )
                        )
                        .ToImmutableArray()
                }
            )
        );
    }

    public async Task<Task<Moongate.Server.Ultima.Types.Books.BookAttachmentClaimResultType>> BeginAsync()
    {
        Task<Moongate.Server.Ultima.Types.Books.BookAttachmentClaimResultType> pending = null!;
        await Books.OnLoopAsync(() => pending = Service.ClaimAsync(Letter.Id, Books.Session));
        return pending;
    }

    public async ValueTask DisposeAsync()
    {
        Store.Continue.TrySetResult();
        if (_ownsBooks) await Books.DisposeAsync();
    }
}
