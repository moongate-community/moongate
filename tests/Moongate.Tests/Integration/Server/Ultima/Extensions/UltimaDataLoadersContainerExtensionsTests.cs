using DryIoc;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Internal;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Data.Templates.StartingItems;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Server.Ultima.Extensions;

public sealed class UltimaDataLoadersContainerExtensionsTests
{
    [Fact]
    public async Task StartAsync_StartingDocumentsResolvePreviouslyLoadedItemsAndBooks()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/items/scroll.toml", "[[item]]\nid = \"readable_scroll\"\nitem_id = 0x14ed\nstackable = false\nscript_id = \"readable_scroll\"\n");
        root.CreateFile("templates/books/welcome_letter.toml", "title = \"Welcome $player_name\"\ncontent = \"Hello $player_name\"\n");
        root.CreateFile("data/starting_items.toml", "[[set]]\ncommon = true\n[[set.items]]\nitems = [\"readable_scroll\"]\nbook_template = \"welcome_letter\"\n");
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["templates", "data"]));
        container.RegisterInstance(new LocalizationConfig());
        container.AddUltimaDataLoaders();
        var registrations = container.Resolve<List<DataLoaderRegistration>>();
        registrations.RemoveAll(registration => registration.EntityType != typeof(ItemTemplate) &&
            registration.EntityType != typeof(BookTemplate) && registration.EntityType != typeof(StartingItemSet));
        var service = new DataLoaderService(container);
        container.RegisterInstance<IDataLoaderService>(service);
        container.Register<IBookTemplateService, BookTemplateService>(Reuse.Singleton);

        await service.StartAsync();

        var entry = Assert.Single(Assert.Single(service.GetEntities<StartingItemSet>()).Items);
        Assert.Equal("welcome_letter", entry.BookTemplate);
        Assert.True(container.Resolve<IBookTemplateService>().TryRender(entry.BookTemplate!,
            new TextTemplateContext { PlayerName = "Aria" }, "eng", entry.BookValues, out var rendered));
        Assert.Equal("Hello Aria", rendered!.Content);
    }
}
