using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.FileLoaders;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.Tests.Server.FileLoaders;

public sealed class FactionTemplateLoaderTests
{
    [Test]
    public async Task LoadAsync_WhenFactionTemplatesExist_ShouldLoadDefinitionsAndEnemyIds()
    {
        using var tempDirectory = new TempDirectory();
        var directoriesConfig = new DirectoriesConfig(
            tempDirectory.Path,
            DirectoryType.Data,
            DirectoryType.Templates,
            DirectoryType.Scripts,
            DirectoryType.Save,
            DirectoryType.Logs,
            DirectoryType.Cache
        );

        var factionsDirectory = Path.Combine(directoriesConfig[DirectoryType.Templates], "factions");
        Directory.CreateDirectory(factionsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(factionsDirectory, "modernuo.json"),
            """
            [
              {
                "type": "faction",
                "id": "true_britannians",
                "name": "True Britannians",
                "category": "factions",
                "description": "Order faction.",
                "enemyFactionIds": ["shadowlords", "minax"]
              },
              {
                "type": "faction",
                "id": "shadowlords",
                "name": "Shadowlords",
                "category": "factions",
                "description": "Chaos faction.",
                "enemyFactionIds": ["true_britannians"]
              }
            ]
            """
        );

        var factionTemplateService = new FactionTemplateService();
        var loader = new FactionTemplateLoader(directoriesConfig, factionTemplateService);

        await loader.LoadAsync();

        Assert.That(factionTemplateService.TryGet("true_britannians", out var faction), Is.True);
        Assert.That(faction, Is.Not.Null);

        Assert.Multiple(
            () =>
            {
                Assert.That(faction!.Name, Is.EqualTo("True Britannians"));
                Assert.That(faction.EnemyFactionIds, Is.EquivalentTo(new[] { "shadowlords", "minax" }));
                Assert.That(factionTemplateService.Count, Is.EqualTo(2));
            }
        );
    }
}
