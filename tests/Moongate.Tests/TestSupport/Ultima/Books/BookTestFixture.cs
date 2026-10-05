using Moongate.Server.Ultima.Services.Items;
using Moongate.Server.Ultima.Interfaces;
using System.Net;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Services.Admin;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Tests.TestSupport.Ultima.Tiles;
using Moongate.Tests.TestSupport.Ultima.Tooltips;
using Moongate.Tests.TestSupport.Ultima.World;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Books;

public sealed class BookTestFixture : IAsyncDisposable
{
    public BroadcastFixture World { get; }
    public StubDataLoaderService Data { get; } = new();
    public StubItemSerialPool Serials { get; } = new();
    public RecordingGumpService Gumps { get; } = new();
    public IScriptEngine Engine { get; set; } = new FakeScriptEngine();
    public InventoryReservationService Reservations { get; }
    public InventoryMutationGuard Inventory { get; }
    public ItemService Items { get; }
    public ItemTemplateService ItemTemplates { get; }
    public ItemHandlingService Handling { get; }
    public BookContextFactory Contexts { get; }
    public BankService Bank { get; }
    public BookDocumentService Books { get; }
    public MobileEntity Player { get; }
    public MobileEntity Other { get; }
    public GameSession Session { get; }
    public ItemEntity Backpack { get; } = new() { Id = new(0x40000001), TemplateId = "backpack", ItemId = 0x0E75, Amount = 1 };
    public BookTemplate Source { get; } = new()
    {
        Id = "welcome_letter", Title = "Welcome $player_name", Author = "British", Content = "Dear $player_name,\n\nBring this to $contact_name.",
        Variables = ["contact_name"]
    };

    private BookTestFixture(BroadcastFixture world, MobileEntity player, MobileEntity other, GameSession session, bool realGumps)
    {
        World = world;
        Player = player;
        Other = other;
        Session = session;
        Data.With(Source).With(
            new ItemTemplate { Id = "backpack", ItemId = new(0x0E75) },
            new ItemTemplate { Id = "readable_scroll", ItemId = new(0x14ED), Stackable = false, ScriptId = "readable_scroll" },
            new ItemTemplate { Id = "jail_release_note", ItemId = new(0x14F0), Stackable = false, ScriptId = "jail_note" },
            new ItemTemplate { Id = "unrelated", ItemId = new(0x14ED), Stackable = false });
        Reservations = new(world.Network.Loop);
        Inventory = new(new Lazy<IItemService>(() => Items!), Reservations);
        Items = TestItems.Create(world.Sectors, loop: world.Network.Loop, inventory: Inventory);
        ItemTemplates = new(Data);
        var tiles = new FakeTileDataService().Item(0x0E75, TileFlagType.Container, 0).Item(0x14ED, TileFlagType.None, 1).Item(0x14F0, TileFlagType.None, 1);
        var factory = new FakeItemFactoryService(ItemTemplates, tiles);
        var tooltips = TestTooltips.Create(Items, world.Mobiles);
        Handling = new(Items, world.Sessions, world.Sender, new RecordingWorldViewService(), tooltips, factory, Serials, inventory: Inventory);
        Bank = new(Items, factory, world.Sessions, world.Mobiles, world.Sender, tooltips, null!, world.Network.Loop, inventory: Inventory, reservations: Reservations);
        var realm = new RealmInstance(new RealmDescriptor("local", 0, "Felucca", IPAddress.Loopback, 2593, AccountType.Regular), Guid.NewGuid());
        Contexts = new(world.Sessions, new AdminServerInfoProvider(ServerMode.Game, realm), realm, new MotdServerIdentity("Moongate"), world.Network.Loop);
        Books = new(new BookTemplateService(Data), Contexts, Items, world.Mobiles, Handling, ItemTemplates, world.Sessions,
            Bank, realGumps ? new GumpService(world.Sender) : Gumps, world.Network.Loop, new(() => Engine), new());
    }

    public static async Task<BookTestFixture> CreateAsync(bool realGumps = false)
    {
        var world = await BroadcastFixture.CreateAsync();
        var session = await world.AddAsync(2);
        await world.AddAsync(3);
        Assert.True(world.Mobiles.TryGet(new(2), out var player));
        Assert.True(world.Mobiles.TryGet(new(3), out var other));
        var fixture = new BookTestFixture(world, player, other, session, realGumps);
        await fixture.OnLoopAsync(() =>
        {
            player.Name = "Pippo";
            other.Name = "Bruno";
            fixture.Backpack.Equip(player.Id, LayerType.Backpack);
            fixture.Items.Add([fixture.Backpack]);
            world.Mobiles.MoveTo(player, MapType.Trammel, new Point3D(1600, 1600, 0));
            world.Mobiles.MoveTo(other, MapType.Trammel, new Point3D(1600, 1600, 0));
            fixture.Serials.Serials.Enqueue(new(0x40000F00));
        });
        return fixture;
    }

    public Task OnLoopAsync(Action action)
    {
        return World.Network.ExecuteOnLoopAsync(action);
    }

    public ItemEntity Give()
    {
        return Assert.IsType<ItemEntity>(Books.Give(Player, "welcome_letter", new Dictionary<string, object?> { ["contact_name"] = "Vega" }));
    }

    public async ValueTask DisposeAsync()
    {
        await World.DisposeAsync();
    }
}
