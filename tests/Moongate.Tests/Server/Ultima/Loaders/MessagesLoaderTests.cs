using Moongate.Core.Directories;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Loaders;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Server.Ultima.Loaders;

public sealed class MessagesLoaderTests
{
    private const string English = """
                                   [messages]
                                   0 = "You board the boat."
                                   1 = "{0} was killed by {1}!"
                                   2 = "Aye, sir."

                                   """;

    private const string Italian = """
                                   [messages]
                                   0 = "Sali a bordo della barca."
                                   1 = "{0} è stato ucciso da {1}!"

                                   """;

    [Fact]
    public async Task InitializeAsync_MissingLanguageFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root, "ita").InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_MissingEnglishFile_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/ita.toml", Italian);

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root, "ita").InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_English_ReadsEveryMessageInOrder()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);

        var messages = (await CreateLoader(root, "eng").LoadDataAsync()).Entities;

        Assert.Equal([0, 1, 2], messages.Select(message => message.Id));
        Assert.Equal("{0} was killed by {1}!", messages[1].Text);
    }

    [Fact]
    public async Task LoadDataAsync_Translation_ReplacesEnglishAndKeepsItForMissingIds()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/ita.toml", Italian);

        var messages = (await CreateLoader(root, "ITA").LoadDataAsync()).Entities.ToDictionary(
            message => message.Id,
            message => message.Text
        );

        Assert.Equal("Sali a bordo della barca.", messages[0]);
        Assert.Equal("{0} è stato ucciso da {1}!", messages[1]);
        Assert.Equal("Aye, sir.", messages[2]);
    }

    [Theory,
     InlineData("0 = \"Sali a bordo della barca.\"", "0 = \"Sali a bordo di {0}.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "7 = \"Sali a bordo della barca.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "zero = \"Sali a bordo della barca.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "0 = \"Sali a bordo {della barca.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "0 = \"Sali a bordo di {16}.\""),
     InlineData("1 = \"{0} è stato ucciso da {1}!\"", "1 = \"{0} è stato ucciso da {2}!\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "0 = \"\"")]
    public async Task LoadDataAsync_InvalidTranslation_ThrowsInvalidDataException(string from, string to)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/ita.toml", Italian.Replace(from, to));

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, "ita").LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_NoEnglishMessages_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", "# nothing\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, "eng").LoadDataAsync());
    }

    [Fact]
    public async Task InitializeAsync_LanguageDirectoryWithTomlFiles_DoesNotThrow()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng/boats.toml", English);
        root.CreateFile("data/messages/ita/boats.toml", Italian);

        await CreateLoader(root, "ita").InitializeAsync();
    }

    [Fact]
    public async Task InitializeAsync_LanguageDirectoryWithoutTomlFiles_ThrowsFileNotFoundException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/ita/readme.txt");

        await Assert.ThrowsAsync<FileNotFoundException>(() => CreateLoader(root, "ita").InitializeAsync());
    }

    [Fact]
    public async Task LoadDataAsync_EnglishDirectory_MergesEveryTomlFile()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng/boats.toml", "[messages]\n0 = \"You board the boat.\"\n");
        root.CreateFile("data/messages/eng/combat.toml", "[messages]\n1 = \"{0} was killed by {1}!\"\n");
        root.CreateFile("data/messages/eng/notes.txt", "[messages]\n9 = \"Not a toml file.\"\n");

        var messages = (await CreateLoader(root, "eng").LoadDataAsync()).Entities;

        Assert.Equal([0, 1], messages.Select(message => message.Id));
        Assert.Equal("{0} was killed by {1}!", messages[1].Text);
    }

    [Fact]
    public async Task LoadDataAsync_FileAndDirectory_MergesBoth()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/eng/shard.toml", "[messages]\n50000 = \"Welcome to the shard.\"\n");

        var messages = (await CreateLoader(root, "eng").LoadDataAsync()).Entities;

        Assert.Equal([0, 1, 2, 50000], messages.Select(message => message.Id));
    }

    [Fact]
    public async Task LoadDataAsync_TranslationDirectory_ReplacesEnglishAndKeepsItForMissingIds()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/ita/boats.toml", "[messages]\n0 = \"Sali a bordo della barca.\"\n");
        root.CreateFile("data/messages/ita/combat.toml", "[messages]\n1 = \"{0} è stato ucciso da {1}!\"\n");

        var messages = (await CreateLoader(root, "ita").LoadDataAsync()).Entities.ToDictionary(
            message => message.Id,
            message => message.Text
        );

        Assert.Equal("Sali a bordo della barca.", messages[0]);
        Assert.Equal("{0} è stato ucciso da {1}!", messages[1]);
        Assert.Equal("Aye, sir.", messages[2]);
    }

    [Fact]
    public async Task LoadDataAsync_SameIdInTwoFiles_ThrowsInvalidDataExceptionNamingBoth()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/eng/shard.toml", "[messages]\n2 = \"Yes, sir.\"\n");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, "eng").LoadDataAsync());

        Assert.Contains("eng.toml", exception.Message);
        Assert.Contains("shard.toml", exception.Message);
    }

    [Theory, InlineData("eng"), InlineData("ita")]
    public async Task LoadDataAsync_SameIdInTwoFilesOfTheDirectory_NamesTheFirstFileByNameAsTheOwner(string duplicated)
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/ita.toml", Italian);
        root.CreateFile($"data/messages/{duplicated}/b.toml", "[messages]\n7 = \"Second.\"\n");
        root.CreateFile($"data/messages/{duplicated}/a.toml", "[messages]\n7 = \"First.\"\n");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, "ita").LoadDataAsync());

        Assert.StartsWith(Path.Join(root.Path, "data", "messages", duplicated, "b.toml"), exception.Message);
        Assert.EndsWith(Path.Join(duplicated, "a.toml") + ".", exception.Message);
    }

    [Fact]
    public async Task LoadDataAsync_Subdirectory_IsIgnored()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/eng/sub/more.toml", "[messages]\n9 = \"Ignored.\"\n");

        var messages = (await CreateLoader(root, "eng").LoadDataAsync()).Entities;

        Assert.Equal([0, 1, 2], messages.Select(message => message.Id));
    }

    [Fact]
    public async Task LoadDataAsync_UpperCaseExtension_IsRead()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", English);
        root.CreateFile("data/messages/eng/Shard.TOML", "[messages]\n9 = \"Welcome.\"\n");

        var messages = (await CreateLoader(root, "eng").LoadDataAsync()).Entities;

        Assert.Equal([0, 1, 2, 9], messages.Select(message => message.Id));
    }

    [Fact]
    public async Task LoadDataAsync_SameIdTwiceInOneFile_ThrowsInvalidDataException()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", "[messages]\n1 = \"One.\"\n01 = \"One again.\"\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, "eng").LoadDataAsync());
    }

    [Fact]
    public async Task LoadDataAsync_NoEnglishMessages_NamesTheFilesRead()
    {
        using var root = new TemporaryDirectory();
        root.CreateFile("data/messages/eng.toml", "# nothing\n");
        root.CreateFile("data/messages/eng/shard.toml", "[message]\n1 = \"Wrong table.\"\n");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => CreateLoader(root, "eng").LoadDataAsync());

        Assert.Contains("eng.toml", exception.Message);
        Assert.Contains("shard.toml", exception.Message);
    }

    private static MessagesLoader CreateLoader(TemporaryDirectory root, string language)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]), new LocalizationConfig { Language = language });
    }
}
