using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Data.Config;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class TextTemplateServiceTests
{
    [Test]
    public void TryRender_WhenPathAttemptsTraversal_ShouldReject()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new TextTemplateService(directoriesConfig, new());

        var success = service.TryRender("../escape.txt", null, out var rendered);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(rendered, Is.EqualTo(string.Empty));
            }
        );
    }

    [Test]
    public void TryRender_WhenTemplateAndModelAreValid_ShouldRenderNestedValuesAndShardDefaults()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsDirectory = Path.Combine(tempDirectory.Path, "scripts");
        var textsDirectory = Path.Combine(scriptsDirectory, "texts");
        Directory.CreateDirectory(textsDirectory);
        var templatePath = Path.Combine(textsDirectory, "welcome_player.txt");
        File.WriteAllText(templatePath, "Welcome to {{ shard.name }}, {{ player.name }}. Website: {{ shard.website_url }}");

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var config = new MoongateConfig
        {
            Game = new() { ShardName = "Test Shard" },
            Http = new() { WebsiteUrl = "https://example.test" }
        };
        var service = new TextTemplateService(directoriesConfig, config);
        var model = new Dictionary<string, object?>
        {
            ["player"] = new Dictionary<string, object?> { ["name"] = "Tommy" }
        };

        var success = service.TryRender("welcome_player.txt", model, out var rendered);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.True);
                Assert.That(rendered, Is.EqualTo("Welcome to Test Shard, Tommy. Website: https://example.test"));
            }
        );
    }

    [Test]
    public void TryRender_WhenTemplateContainsHashComments_ShouldSkipCommentsAndPreserveEscapedHash()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsDirectory = Path.Combine(tempDirectory.Path, "scripts");
        var textsDirectory = Path.Combine(scriptsDirectory, "texts");
        Directory.CreateDirectory(textsDirectory);
        File.WriteAllText(
            Path.Combine(textsDirectory, "comments.txt"),
            """
            # hidden line
            Welcome to {{ shard.name }} # internal note
            Price: 100\# coins
            """
        );

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var config = new MoongateConfig
        {
            Game = new() { ShardName = "Test Shard" }
        };
        var service = new TextTemplateService(directoriesConfig, config);

        var success = service.TryRender("comments.txt", null, out var rendered);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.True);
                Assert.That(rendered, Is.EqualTo("Welcome to Test Shard\nPrice: 100# coins"));
            }
        );
    }

    [Test]
    public void TryRender_WhenTemplateIsMissing_ShouldReturnFalse()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new TextTemplateService(directoriesConfig, new());

        var success = service.TryRender("missing.txt", null, out var rendered);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(rendered, Is.EqualTo(string.Empty));
            }
        );
    }

    [Test]
    public void TryRender_WhenTemplateSyntaxIsInvalid_ShouldReturnFalse()
    {
        using var tempDirectory = new TempDirectory();
        var scriptsDirectory = Path.Combine(tempDirectory.Path, "scripts");
        var textsDirectory = Path.Combine(scriptsDirectory, "texts");
        Directory.CreateDirectory(textsDirectory);
        File.WriteAllText(Path.Combine(textsDirectory, "broken.txt"), "Hello {{ player.name ");

        var directoriesConfig = new DirectoriesConfig(tempDirectory.Path, Enum.GetNames<DirectoryType>());
        var service = new TextTemplateService(directoriesConfig, new());

        var success = service.TryRender("broken.txt", null, out var rendered);

        Assert.Multiple(
            () =>
            {
                Assert.That(success, Is.False);
                Assert.That(rendered, Is.EqualTo(string.Empty));
            }
        );
    }
}
