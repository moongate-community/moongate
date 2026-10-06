using DryIoc;
using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class BooksLoaderTests
{
    private const string Valid = "title = \"Welcome $player_name\"\nauthor = \"British\"\ncontent = \"\"\"\nCaro ${player_name}, è un piacere!\n\nHello\n\"\"\"\n";

    [Fact]
    public async Task RegisteredLoader_UsesAlreadyLoadedItems()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/welcome_letter.toml", Valid);
        using var container = new Container();
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["templates"]));
        container.RegisterInstance<IDataLoaderService>(new StubDataLoaderService().With(
            new ItemTemplate { Id = "readable_scroll", Stackable = false, ScriptId = "readable_scroll" }));
        container.AddUltimaDataLoaders();
        var book = Assert.Single((await container.Resolve<IDataLoader<BookTemplate>>().LoadDataAsync()).Entities);
        Assert.Equal("welcome_letter", book.Id);
    }

    [Fact]
    public async Task LoadDataAsync_UnicodeParagraphsAndFilename_ArePreserved()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/welcome_letter.toml", Valid.Replace("\n", "\r\n"));
        var book = Assert.Single((await Loader(root).LoadDataAsync()).Entities);
        Assert.Equal("welcome_letter", book.Id);
        Assert.Equal("Caro ${player_name}, è un piacere!\n\nHello\n", book.Content.Replace("\r\n", "\n"));
        Assert.Equal("readable_scroll", book.ItemTemplate);
    }

    [Fact]
    public async Task LoadDataAsync_MissingDirectory_IsEmpty()
    {
        using var root = new TemporaryDirectory();
        Assert.Empty((await Loader(root).LoadDataAsync()).Entities);
    }

    [Fact]
    public async Task LoadDataAsync_DuplicateStems_IdentifiesBothFiles()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/a/welcome_letter.toml", Valid);
        root.CreateFile("templates/books/b/welcome_letter.toml", Valid);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync());
        Assert.Contains("a/welcome_letter.toml", error.Message);
        Assert.Contains("b/welcome_letter.toml", error.Message);
    }

    [Theory]
    [InlineData("title = \"\"", "content = \"Hello\"")]
    [InlineData("title = \"Title\"", "content = \" \"")]
    [InlineData("title = \"Title\"", "content = \"$dayz\"")]
    [InlineData("title = \"Title\"", "content = \"${Player}\"")]
    [InlineData("title = \"Title\"", "content = \"Hello\"\nvariables = [\"player_name\"]")]
    [InlineData("title = \"Title\"", "content = \"Hello\"\nvariables = [\"x\", \"x\"]")]
    [InlineData("title = \"Title\"", "content = \"Hello\"\nvariables = [\"X\"]")]
    [InlineData("title = \"Title\"", "content = \"Hello\"\nitem_template = \"missing\"")]
    [InlineData("title = \"Title\"", "content = \"Hello\"\n[translations.zzz]\ncontent = \"Hello\"")]
    [InlineData("title = \"Title\"", "content = \"Hello\"\n[translations.ita]\ncontent = \"$dayz\"")]
    [InlineData("title =", "content = \"Hello\"")]
    [InlineData("title = \"Title\"", "content = \"Hello\\u0000\"")]
    public async Task LoadDataAsync_InvalidSource_FailsWithPath(string title, string body)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/bad.toml", title + "\n" + body);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync());
        Assert.Contains("books/bad.toml", error.Message);
    }

    [Theory]
    [InlineData(true, "readable_scroll")]
    [InlineData(false, "unrelated")]
    public async Task LoadDataAsync_UnsupportedItem_FailsWithPath(bool stackable, string script)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/note.toml", Valid);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root, stackable, script).LoadDataAsync());
        Assert.Contains("note.toml", error.Message);
    }

    [Theory]
    [InlineData(16384, true)]
    [InlineData(16385, false)]
    public async Task LoadDataAsync_SourceContentLength_EnforcesLimit(int length, bool valid)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/note.toml", "title = \"Note\"\ncontent = \"" + new string('a', length) + "\"");
        if (valid)
        {
            Assert.Single((await Loader(root).LoadDataAsync()).Entities);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync());
        }
    }

    [Fact]
    public async Task LoadDataAsync_ItemId_IsTheGraphicOfTheItem_AndNoneWithoutIt()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/blue.toml", "item_id = 0x0FF2\n" + Valid);
        root.CreateFile("templates/books/plain.toml", Valid);

        var books = (await Loader(root).LoadDataAsync()).Entities;

        Assert.Equal(0x0FF2, books.Single(book => book.Id == "blue").ItemId);
        Assert.Null(books.Single(book => book.Id == "plain").ItemId);
    }

    [Theory]
    [InlineData("item_id = 0\n", "")]
    [InlineData("item_id = 0x10000\n", "")]
    [InlineData("item_id = -1\n", "")]
    // The graphic is the item's, whatever the language of its text.
    [InlineData("", "\n[translations.ita]\ntitle = \"Benvenuto\"\nitem_id = 0x0FF2\n")]
    public async Task LoadDataAsync_AnItemIdOutOfRange_OrInATranslation_FailsWithPath(string before, string after)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/bad.toml", before + Valid + after);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root).LoadDataAsync());

        Assert.Contains("books/bad.toml", error.Message);
    }

    // A book has no button to claim them with.
    [Fact]
    public async Task LoadDataAsync_AttachmentsOnABook_AreRefused_AndAllowedOnAScroll()
    {
        const string gift = "\n[[attachments]]\nitem_template = \"bread\"\n";
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/letter.toml", Valid + gift);
        var items = new StubDataLoaderService().With(
            new ItemTemplate { Id = "readable_scroll", Stackable = false, ScriptId = "readable_scroll" },
            new ItemTemplate { Id = "readable_book", Stackable = false, ScriptId = "readable_book" },
            new ItemTemplate { Id = "bread", Stackable = false });
        var directories = new DirectoriesConfig(root.Path, ["templates"]);

        Assert.Single((await new BooksLoader(directories, items).LoadDataAsync()).Entities);

        root.CreateFile("templates/books/tome.toml", "item_template = \"readable_book\"\n" + Valid + gift);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new BooksLoader(directories, items).LoadDataAsync());

        Assert.Contains("tome.toml", error.Message);
    }

    // A blank book a player writes in: no text of its own, and as many pages as it says.
    [Fact]
    public async Task LoadDataAsync_AWritableBook_MayBeBlank_AndSaysItsPages()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/blank.toml", "title = \"a book\"\nauthor = \"$player_name\"\nwritable = true\npages = 30\n");
        root.CreateFile("templates/books/diary.toml", "title = \"Diary\"\nwritable = true\ncontent = \"Day one.\"\n");

        var books = (await Loader(root, script: "readable_book").LoadDataAsync()).Entities;

        var blank = books.Single(book => book.Id == "blank");
        Assert.Equal((true, 30, ""), (blank.Writable, blank.Pages, blank.Content));
        Assert.Equal((true, (int?)null, "Day one."), (books.Single(book => book.Id == "diary").Writable, books.Single(book => book.Id == "diary").Pages, books.Single(book => book.Id == "diary").Content));
    }

    [Theory]
    // A scroll is not written in.
    [InlineData("readable_scroll", "title = \"T\"\ncontent = \"x\"\nwritable = true\n")]
    // Only a book that is written in has a number of pages, and only a writable one may be blank.
    [InlineData("readable_book", "title = \"T\"\ncontent = \"x\"\npages = 20\n")]
    [InlineData("readable_book", "title = \"T\"\n")]
    [InlineData("readable_book", "title = \"T\"\nwritable = true\npages = 0\n")]
    [InlineData("readable_book", "title = \"T\"\nwritable = true\npages = 256\n")]
    [InlineData("readable_book", "title = \"\"\nwritable = true\n")]
    public async Task LoadDataAsync_AWritableSourceThatIsWrong_FailsWithPath(string script, string source)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/bad.toml", source);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Loader(root, script: script).LoadDataAsync());

        Assert.Contains("books/bad.toml", error.Message);
    }

    [Fact]
    public async Task LoadDataAsync_ABookItem_IsAReadableItem()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/tome.toml", Valid);

        Assert.Single((await Loader(root, script: "readable_book").LoadDataAsync()).Entities);
    }

    private static BooksLoader Loader(TemporaryDirectory root, bool stackable = false, string script = "readable_scroll")
    {
        return new(new DirectoriesConfig(root.Path, ["templates"]),
            new StubDataLoaderService().With(new ItemTemplate { Id = "readable_scroll", Stackable = stackable, ScriptId = script }));
    }

    [Theory]
    [InlineData("amount = 0")]
    [InlineData("amount = 65536")]
    [InlineData("amount = \"1d2-1\"")]
    [InlineData("amount = 33")]
    [InlineData("hue = \"broken\"")]
    [InlineData("item_template = \"missing\"")]
    public async Task LoadDataAsync_InvalidAttachment_RefusesSourceWithPath(string field)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("templates/books/gift.toml", Valid + "\n[[attachments]]\n" +
            (field.StartsWith("item_template", StringComparison.Ordinal) ? field : "item_template = \"bread\"\n" + field));
        var loader = new BooksLoader(new DirectoriesConfig(root.Path, ["templates"]), new StubDataLoaderService().With(
            new ItemTemplate { Id = "readable_scroll", Stackable = false, ScriptId = "readable_scroll" },
            new ItemTemplate { Id = "bread", Stackable = false }));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => loader.LoadDataAsync());
        Assert.Contains("gift.toml", error.Message);
    }
}
