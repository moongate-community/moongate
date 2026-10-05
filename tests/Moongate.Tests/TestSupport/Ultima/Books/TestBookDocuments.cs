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
    public static async Task<BookDocumentService> CreateAsync(BroadcastFixture world, IItemService items,
        IItemHandlingService handling, IItemTemplateService itemTemplates, IGameLoopService loop,
        IGumpService? gumps = null)
    {
        var source = new StubDataLoaderService().With(
            new ItemTemplate { Id = "readable_scroll", Stackable = false, ScriptId = "readable_scroll" },
            new ItemTemplate { Id = "jail_release_note", Stackable = false, ScriptId = "jail_note" });
        var directories = new DirectoriesConfig(Path.Combine(BookLuaFixture.RepositoryRoot(), "moongate_root"), ["templates"]);
        var books = (await new BooksLoader(directories, source).LoadDataAsync()).Entities.ToArray();
        var realm = new RealmInstance(new RealmDescriptor("local", 0, "Felucca", IPAddress.Loopback, 2593, AccountType.Regular), Guid.NewGuid());
        var contexts = new BookContextFactory(world.Sessions, new AdminServerInfoProvider(ServerMode.Game, realm), realm, new MotdServerIdentity("Moongate"), loop);
        return new(new BookTemplateService(new StubDataLoaderService().With(books)), contexts, items, world.Mobiles,
            handling, itemTemplates, world.Sessions, new StubBankService(), gumps ?? new RecordingGumpService(),
            loop, new(() => new FakeScriptEngine()), new());
    }
}
