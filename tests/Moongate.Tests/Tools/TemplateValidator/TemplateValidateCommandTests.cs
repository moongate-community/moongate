using Moongate.Tests.TestSupport;
using Moongate.TemplateValidator.Commands;
using Moongate.TemplateValidator.Services;
using Spectre.Console;

namespace Moongate.Tests.Tools.TemplateValidator;

public class TemplateValidateCommandTests
{
    [Test]
    public async Task RunAsync_WhenRootDirectoryIsMissing_ShouldReturnFailure()
    {
        using var writer = new StringWriter();
        var command = CreateCommand(writer);

        var exitCode = await command.RunAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Multiple(
            () =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(writer.ToString(), Does.Contain("Template validation failed"));
            }
        );
    }

    [Test]
    public async Task RunAsync_WhenValidationSucceeds_ShouldReturnSuccess()
    {
        using var tempDirectory = new TempDirectory();
        using var writer = new StringWriter();
        var rootDirectory = await CreateValidRootAsync(tempDirectory);
        var command = CreateCommand(writer);

        var exitCode = await command.RunAsync(rootDirectory);

        Assert.Multiple(
            () =>
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(writer.ToString(), Does.Contain("Template validation completed successfully"));
            }
        );
    }

    private static TemplateValidateCommand CreateCommand(StringWriter writer)
    {
        var console = AnsiConsole.Create(
            new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer)
            }
        );

        return new()
        {
            Runner = new TemplateValidationRunner(),
            ConsoleWriter = new TemplateValidationConsoleWriter(console)
        };
    }

    private static async Task<string> CreateValidRootAsync(TempDirectory tempDirectory)
    {
        var rootDirectory = Path.Combine(tempDirectory.Path, "moongate");
        var containersDirectory = Path.Combine(rootDirectory, "data", "containers");
        var itemsDirectory = Path.Combine(rootDirectory, "templates", "items");
        var mobilesDirectory = Path.Combine(rootDirectory, "templates", "mobiles");

        Directory.CreateDirectory(containersDirectory);
        Directory.CreateDirectory(itemsDirectory);
        Directory.CreateDirectory(mobilesDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(containersDirectory, "default_containers.json"),
            """
            [
              { "Id": "backpack", "ItemId": 3701, "Width": 7, "Height": 4, "Name": "Backpack" }
            ]
            """
        );

        await File.WriteAllTextAsync(
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

        await File.WriteAllTextAsync(
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

        return rootDirectory;
    }
}
