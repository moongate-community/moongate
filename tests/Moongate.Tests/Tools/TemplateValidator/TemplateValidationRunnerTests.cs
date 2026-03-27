using Moongate.Tests.TestSupport;
using Moongate.TemplateValidator.Services;

namespace Moongate.Tests.Tools.TemplateValidator;

public class TemplateValidationRunnerTests
{
    [Test]
    public async Task ValidateAsync_WhenRootContainsInvalidTemplates_ShouldReturnFailure()
    {
        using var tempDirectory = new TempDirectory();
        var rootDirectory = await CreateInvalidRootAsync(tempDirectory);
        var runner = new TemplateValidationRunner();

        var result = await runner.ValidateAsync(rootDirectory);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.Summary, Does.Contain("Template validation failed"));
                Assert.That(result.Errors, Is.Not.Empty);
                Assert.That(result.Errors.Any(static error => error.Contains("has no variants", StringComparison.Ordinal)), Is.True);
            }
        );
    }

    [Test]
    public async Task ValidateAsync_WhenRootContainsValidTemplates_ShouldReturnSuccess()
    {
        using var tempDirectory = new TempDirectory();
        var rootDirectory = await CreateValidRootAsync(tempDirectory);
        var runner = new TemplateValidationRunner();

        var result = await runner.ValidateAsync(rootDirectory);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(0));
                Assert.That(result.ItemTemplateCount, Is.EqualTo(1));
                Assert.That(result.MobileTemplateCount, Is.EqualTo(1));
                Assert.That(result.Errors, Is.Empty);
            }
        );
    }

    [Test]
    public async Task ValidateAsync_ShouldNotRequireUoDirectory()
    {
        using var tempDirectory = new TempDirectory();
        var rootDirectory = await CreateValidRootAsync(tempDirectory);
        var previous = Environment.GetEnvironmentVariable("MOONGATE_UO_DIRECTORY");
        var runner = new TemplateValidationRunner();

        Environment.SetEnvironmentVariable("MOONGATE_UO_DIRECTORY", null);

        try
        {
            var result = await runner.ValidateAsync(rootDirectory);

            Assert.That(result.ExitCode, Is.EqualTo(0));
        }
        finally
        {
            Environment.SetEnvironmentVariable("MOONGATE_UO_DIRECTORY", previous);
        }
    }

    [Test]
    public async Task ValidateAsync_WhenRootDirectoryDoesNotExist_ShouldReturnFailure()
    {
        var runner = new TemplateValidationRunner();
        var missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = await runner.ValidateAsync(missingRoot);

        Assert.Multiple(
            () =>
            {
                Assert.That(result.ExitCode, Is.EqualTo(1));
                Assert.That(result.Summary, Does.Contain("does not exist"));
                Assert.That(result.Errors, Is.Not.Empty);
            }
        );
    }

    private static async Task<string> CreateInvalidRootAsync(TempDirectory tempDirectory)
    {
        var rootDirectory = CreateRootDirectory(tempDirectory);
        await WriteContainersAsync(rootDirectory);
        await WriteItemsAsync(rootDirectory);
        await WriteInvalidMobilesAsync(rootDirectory);

        return rootDirectory;
    }

    private static async Task<string> CreateValidRootAsync(TempDirectory tempDirectory)
    {
        var rootDirectory = CreateRootDirectory(tempDirectory);
        await WriteContainersAsync(rootDirectory);
        await WriteItemsAsync(rootDirectory);
        await WriteValidMobilesAsync(rootDirectory);

        return rootDirectory;
    }

    private static string CreateRootDirectory(TempDirectory tempDirectory)
    {
        var rootDirectory = Path.Combine(tempDirectory.Path, "moongate");
        Directory.CreateDirectory(rootDirectory);

        return rootDirectory;
    }

    private static Task WriteContainersAsync(string rootDirectory)
    {
        var containersDirectory = Path.Combine(rootDirectory, "data", "containers");
        Directory.CreateDirectory(containersDirectory);

        return File.WriteAllTextAsync(
            Path.Combine(containersDirectory, "default_containers.json"),
            """
            [
              { "Id": "backpack", "ItemId": 3701, "Width": 7, "Height": 4, "Name": "Backpack" }
            ]
            """
        );
    }

    private static Task WriteItemsAsync(string rootDirectory)
    {
        var itemsDirectory = Path.Combine(rootDirectory, "templates", "items");
        Directory.CreateDirectory(itemsDirectory);

        return File.WriteAllTextAsync(
            Path.Combine(itemsDirectory, "items.json"),
            """
            [
              {
                "type": "item",
                "category": "clothes",
                "id": "shirt",
                "name": "Shirt",
                "description": "shirt",
                "container": [],
                "dyeable": false,
                "goldValue": "0",
                "hue": "0",
                "isMovable": true,
                "itemId": "0x1517",
                "lootType": "Regular",
                "scriptId": "items.shirt",
                "tags": [],
                "weight": 1.0
              }
            ]
            """
        );
    }

    private static Task WriteInvalidMobilesAsync(string rootDirectory)
    {
        var mobilesDirectory = Path.Combine(rootDirectory, "templates", "mobiles");
        Directory.CreateDirectory(mobilesDirectory);

        return File.WriteAllTextAsync(
            Path.Combine(mobilesDirectory, "mobiles.json"),
            """
            [
              {
                "type": "mobile",
                "category": "test",
                "id": "variantless_mobile",
                "title": "variantless mobile",
                "name": "variantless mobile",
                "description": "invalid"
              }
            ]
            """
        );
    }

    private static Task WriteValidMobilesAsync(string rootDirectory)
    {
        var mobilesDirectory = Path.Combine(rootDirectory, "templates", "mobiles");
        Directory.CreateDirectory(mobilesDirectory);

        return File.WriteAllTextAsync(
            Path.Combine(mobilesDirectory, "mobiles.json"),
            """
            [
              {
                "type": "mobile",
                "category": "test",
                "id": "orc",
                "title": "an orc",
                "name": "orc",
                "description": "valid",
                "ai": {
                  "brain": "ai_melee",
                  "fightMode": "closest",
                  "rangePerception": 10,
                  "rangeFight": 1
                },
                "variants": [
                  {
                    "name": "default",
                    "appearance": {
                      "body": "0x0190",
                      "skinHue": 0,
                      "hairHue": 0
                    }
                  }
                ]
              }
            ]
            """
        );
    }
}
