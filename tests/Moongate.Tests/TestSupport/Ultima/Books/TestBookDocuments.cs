using System.Net;
using Moongate.Core.Directories;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Services.Admin;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Ultima.Bank;
using Moongate.Tests.TestSupport.Ultima.Gumps;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.TestSupport.Ultima.Books;

public static class TestBookDocuments
{
    /// <summary>
    ///     The item templates the shipped documents of <c>templates/books</c> name: what they are written on and what
    ///     their attachments give. A shipped document that names another item needs it here.
    /// </summary>
    public static ItemTemplate[] ShippedItems()
    {
        return
        [
            new() { Id = "readable_scroll", ItemId = new(0x14ED), Stackable = false, ScriptId = "readable_scroll" },
            new() { Id = "readable_book", ItemId = new(0x0FF1), Stackable = false, ScriptId = "readable_book" },
            new() { Id = "jail_release_note", ItemId = new(0x14F0), Stackable = false, ScriptId = "jail_note" },
            new() { Id = "0x0eed_gold_coin", ItemId = new(0x0EED), Stackable = true }
        ];
    }

    public static async Task<BookDocumentService> CreateAsync(
        BroadcastFixture world, IItemService items,
        IItemHandlingService handling, IItemTemplateService itemTemplates, IGameLoopService loop,
        IGumpService? gumps = null
    )
    {
        var source = new StubDataLoaderService().With(ShippedItems());
        var directories = new DirectoriesConfig(
            Path.Combine(BookLuaFixture.RepositoryRoot(), "moongate_root"),
            ["templates"]
        );
        var books = (await new BooksLoader(directories, source).LoadDataAsync()).Entities.ToArray();
        var realm = new RealmInstance(
            new RealmDescriptor("local", 0, "Felucca", IPAddress.Loopback, 2593, AccountType.Regular),
            Guid.NewGuid()
        );
        var contexts = new BookContextFactory(
            world.Sessions,
            new AdminServerInfoProvider(ServerMode.Game, realm),
            realm,
            new MotdServerIdentity("Moongate"),
            loop
        );
        return new(
            new BookTemplateService(new StubDataLoaderService().With(books)),
            contexts,
            items,
            world.Mobiles,
            handling,
            itemTemplates,
            world.Sessions,
            new StubBankService(),
            gumps ?? new RecordingGumpService(),
            loop,
            new(() => new FakeScriptEngine()),
            new()
        );
    }
}
