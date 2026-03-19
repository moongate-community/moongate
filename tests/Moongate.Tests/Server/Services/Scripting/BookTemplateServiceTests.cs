using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class BookTemplateServiceTests
{
    [Test]
    public void TryLoad_WhenAuthorIsMissing_ShouldReturnFalse()
    {
        using var tempDirectory = new TempDirectory();
        var booksDirectory = Path.Combine(tempDirectory.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        File.WriteAllText(
            Path.Combine(booksDirectory, "missing_author.txt"),
            """
            [Title] Hello

            Welcome.
            """
        );

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new BookTemplateService(directoriesConfig, new());

        var success = service.TryLoad("missing_author", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(book, Is.Null);
            }
        );
    }

    [Test]
    public void TryLoad_WhenBookTemplateAttemptsTraversal_ShouldReject()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new BookTemplateService(directoriesConfig, new());

        var success = service.TryLoad("../escape", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(book, Is.Null);
            }
        );
    }

    [Test]
    public void TryLoad_WhenBookTemplateIsMissing_ShouldReturnFalse()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new BookTemplateService(directoriesConfig, new());

        var success = service.TryLoad("missing_book", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(book, Is.Null);
            }
        );
    }

    [Test]
    public void TryLoad_WhenBookTemplateIsValid_ShouldParseMetadataAndRenderBody()
    {
        using var tempDirectory = new TempDirectory();
        var booksDirectory = Path.Combine(tempDirectory.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        File.WriteAllText(
            Path.Combine(booksDirectory, "welcome_player.txt"),
            """
            # hidden
            [Title] Welcome To Moongate
            [Author] Tommy
            [ReadOnly] True

            Welcome to {{ shard.name }}.
            Website: {{ shard.website_url }}
            Price: 100\# coins
            """
        );

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var config = new MoongateConfig
        {
            Game = new() { ShardName = "Test Shard" },
            Http = new() { WebsiteUrl = "https://example.test" }
        };
        var service = new BookTemplateService(directoriesConfig, config);

        var success = service.TryLoad("welcome_player", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.True);
                Assert.That(book, Is.Not.Null);
                Assert.That(book!.Title, Is.EqualTo("Welcome To Moongate"));
                Assert.That(book.Author, Is.EqualTo("Tommy"));
                Assert.That(book.ReadOnly, Is.True);
                Assert.That(
                    book.Content,
                    Is.EqualTo("Welcome to Test Shard.\nWebsite: https://example.test\nPrice: 100# coins")
                );
            }
        );
    }

    [Test]
    public void TryLoad_WhenReadOnlyIsFalse_ShouldParseReadOnlyFlag()
    {
        using var tempDirectory = new TempDirectory();
        var booksDirectory = Path.Combine(tempDirectory.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        File.WriteAllText(
            Path.Combine(booksDirectory, "journal.txt"),
            """
            [Title] Journal
            [Author] Scribe
            [ReadOnly] False

            Entry one.
            """
        );

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new BookTemplateService(directoriesConfig, new());

        var success = service.TryLoad("journal", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.True);
                Assert.That(book, Is.Not.Null);
                Assert.That(book!.ReadOnly, Is.False);
            }
        );
    }

    [Test]
    public void TryLoad_WhenReadOnlyIsMissing_ShouldLeaveReadOnlyUnset()
    {
        using var tempDirectory = new TempDirectory();
        var booksDirectory = Path.Combine(tempDirectory.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        File.WriteAllText(
            Path.Combine(booksDirectory, "plain_book.txt"),
            """
            [Title] Plain Book
            [Author] Scribe

            Entry one.
            """
        );

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new BookTemplateService(directoriesConfig, new());

        var success = service.TryLoad("plain_book", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.True);
                Assert.That(book, Is.Not.Null);
                Assert.That(book!.ReadOnly, Is.Null);
            }
        );
    }

    [Test]
    public void TryLoad_WhenTitleIsMissing_ShouldReturnFalse()
    {
        using var tempDirectory = new TempDirectory();
        var booksDirectory = Path.Combine(tempDirectory.Path, "templates", "books");
        Directory.CreateDirectory(booksDirectory);
        File.WriteAllText(
            Path.Combine(booksDirectory, "missing_title.txt"),
            """
            [Author] Tommy

            Welcome.
            """
        );

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new BookTemplateService(directoriesConfig, new());

        var success = service.TryLoad("missing_title", null, out var book);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(book, Is.Null);
            }
        );
    }
}
