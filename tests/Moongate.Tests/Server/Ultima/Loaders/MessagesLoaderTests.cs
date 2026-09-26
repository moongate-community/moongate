using Moongate.Core.Directories;
using Moongate.Server.Core.Data.Config;
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

        var messages = (await CreateLoader(root, "ITA").LoadDataAsync()).Entities.ToDictionary(message => message.Id, message => message.Text);

        Assert.Equal("Sali a bordo della barca.", messages[0]);
        Assert.Equal("{0} è stato ucciso da {1}!", messages[1]);
        Assert.Equal("Aye, sir.", messages[2]);
    }

    [Theory,
     InlineData("0 = \"Sali a bordo della barca.\"", "0 = \"Sali a bordo di {0}.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "7 = \"Sali a bordo della barca.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "zero = \"Sali a bordo della barca.\""),
     InlineData("0 = \"Sali a bordo della barca.\"", "0 = \"Sali a bordo {della barca.\""),
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

    private static MessagesLoader CreateLoader(TemporaryDirectory root, string language)
    {
        return new(new DirectoriesConfig(root.Path, ["data"]), new LocalizationConfig { Language = language });
    }
}
