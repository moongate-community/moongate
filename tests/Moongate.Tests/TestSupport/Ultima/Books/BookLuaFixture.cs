using DryIoc;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Events;
using Moongate.Scripting.Extensions.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.TestSupport.Ultima.Books;

public sealed class BookLuaFixture : IAsyncDisposable
{
    private readonly Container _container = new();
    private readonly TemporaryScriptsDirectory _scripts = new();

    public BookTestFixture Documents { get; }
    public LuaScriptEngineService Engine { get; private set; } = null!;
    public ItemScriptService ItemScripts { get; private set; } = null!;
    public List<ScriptErrorEvent> Errors { get; } = [];

    private BookLuaFixture(BookTestFixture documents)
    {
        Documents = documents;
    }

    public static async Task<BookLuaFixture> CreateAsync(bool realGumps = false, string? jailScript = null)
    {
        var fixture = new BookLuaFixture(await BookTestFixture.CreateAsync(realGumps));
        var root = RepositoryRoot();
        fixture._scripts.Write("items/readable_scroll.lua", await File.ReadAllTextAsync(Path.Combine(root, "moongate_root/scripts/items/readable_scroll.lua")));
        fixture._scripts.Write("items/jail_note.lua", jailScript ?? await File.ReadAllTextAsync(Path.Combine(root, "moongate_root/scripts/items/jail_note.lua")));
        fixture._scripts.Write("init.lua", """
            function make_letter()
                return book.give(2, "welcome_letter", { contact_name = "Vega" })
            end
            function missing_values()
                return book.give(2, "welcome_letter")
            end
            function use_letter(serial)
                return book.open(serial, 2)
            end
            """);
        var options = new ScriptEngineOptions { ScriptsDirectory = fixture._scripts.Path, WriteDefinitions = false };
        var container = fixture._container;
        container.RegisterMoongateEventBus();
        container.RegisterInstance<IBookDocumentService>(fixture.Documents.Books);
        container.RegisterInstance<IItemService>(fixture.Documents.Items);
        container.RegisterInstance<IMobileService>(fixture.Documents.World.Mobiles);
        container.AddScriptModule<BookModule>();
        container.Resolve<IMoongateEventBus>().Subscribe<ScriptErrorEvent>((error, _) =>
        {
            fixture.Errors.Add(error);
            return Task.CompletedTask;
        });
        fixture.Engine = new(options, container.Resolve<IScriptModuleRegistry>(), container,
            fixture.Documents.World.Network.Loop, new RecordingTimerService(), new EventBusAdapter(container));
        fixture.Documents.Engine = fixture.Engine;
        await fixture.Engine.StartAsync();
        fixture.ItemScripts = new(fixture.Engine, fixture.Documents.ItemTemplates, fixture.Documents.World.Network.Loop, options);
        await fixture.ItemScripts.StartAsync();
        return fixture;
    }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    public async ValueTask DisposeAsync()
    {
        await ItemScripts.StopAsync();
        Engine.Dispose();
        _container.Dispose();
        _scripts.Dispose();
        await Documents.DisposeAsync();
    }
}
